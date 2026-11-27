import { createHmac } from "node:crypto";

import type { AppKind } from "./auth-env";

const ALIAS = /^[A-Za-z0-9._-]{1,128}$/;

/**
 * Decodes a compact JWT payload for narrowing only. Better Auth already
 * obtained the token through the verified callback; the gateway remains the
 * signature, lifetime, membership and authorization authority.
 */
export function readJwtClaims(token: string): Record<string, unknown> | null {
  if (token.length === 0 || token.length > 16_384) return null;
  const parts = token.split(".");
  if (parts.length !== 3 || !parts[1]) return null;
  try {
    const payload: unknown = JSON.parse(Buffer.from(parts[1], "base64url").toString("utf8"));
    return payload !== null && typeof payload === "object" && !Array.isArray(payload)
      ? (payload as Record<string, unknown>)
      : null;
  } catch {
    return null;
  }
}

export function parseOrganizationAlias(value: unknown): string | null {
  if (typeof value !== "string") return null;
  const alias = value.trim();
  return ALIAS.test(alias) ? alias : null;
}

/** A reference that starts a single path segment on this app's origin. */
function isSameAppReference(reference: string): boolean {
  if (!reference.startsWith("/") || reference.startsWith("//")) return false;
  if (reference.includes("\\")) return false;
  return !Array.from(reference).some((char) => {
    const code = char.charCodeAt(0);
    return code < 32 || code === 127;
  });
}

/**
 * Only a relative path inside this app may receive the browser after sign-in.
 *
 * The serialized reference is re-resolved exactly as the browser consumes it.
 * URL path normalization can turn a same-origin parse such as `/.//host` into
 * the protocol-relative `//host`, so the final destination, not only the
 * parsed input, must remain on the configured app origin.
 */
export function safeReturnTo(value: unknown, baseURL: string): string {
  if (typeof value !== "string" || value.length === 0 || value.length > 512) return "/";
  if (!isSameAppReference(value)) return "/";
  try {
    const base = new URL(baseURL);
    const resolved = new URL(value, base);
    if (resolved.origin !== base.origin) return "/";
    const target = `${resolved.pathname}${resolved.search}${resolved.hash}`;
    if (!isSameAppReference(target)) return "/";
    const final = new URL(target, base);
    if (final.origin !== base.origin) return "/";
    const reference = `${final.pathname}${final.search}${final.hash}`;
    return isSameAppReference(reference) ? reference : "/";
  } catch {
    return "/";
  }
}

export class CrossOriginActionError extends Error {
  constructor() {
    super("Sign-in and sign-out requests must come from this app's origin.");
    this.name = "CrossOriginActionError";
  }
}

/**
 * Server Actions are exposed as POST endpoints. Next validates its action
 * origin as well; this check pins the same configured app origin explicitly.
 */
export function assertSameOrigin(headers: Headers, baseURL: string): void {
  const expected = new URL(baseURL).origin;
  const origin = headers.get("origin");
  if (origin !== null) {
    if (origin !== expected) throw new CrossOriginActionError();
    return;
  }
  if (headers.get("sec-fetch-site") !== "same-origin") throw new CrossOriginActionError();
}

function opaqueScope(secret: string, parts: readonly string[]): string {
  return createHmac("sha256", secret).update(JSON.stringify(parts)).digest("base64url");
}

/** A valid session's own scope for cursor history; never provider claims. */
export function paginationSessionScope(
  secret: string,
  app: AppKind,
  issuer: string,
  userId: string,
  sessionId: string,
  organizationId: string | null,
): string {
  return opaqueScope(secret, [
    "pagination-session-scope:v1",
    app,
    issuer,
    userId,
    sessionId,
    organizationId ?? "",
  ]);
}
