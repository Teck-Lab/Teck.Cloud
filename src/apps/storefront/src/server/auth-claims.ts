import { createHmac } from "node:crypto";

import type { AppKind } from "./auth-env";

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const ALIAS = /^[A-Za-z0-9._-]{1,128}$/;

export interface OrganizationAccount {
  readonly providerId: string;
  readonly accessToken?: string | null;
}

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

/** Restricted to the gateway's single rich-format organization claim with a UUID id. */
export function singleOrganizationId(claims: Record<string, unknown>): string | null {
  const organization = claims.organization;
  if (organization === null || typeof organization !== "object" || Array.isArray(organization)) {
    return null;
  }
  const aliases = Object.keys(organization);
  if (aliases.length !== 1) return null;
  const membership = (organization as Record<string, unknown>)[aliases[0] as string];
  if (membership === null || typeof membership !== "object" || Array.isArray(membership)) {
    return null;
  }
  const id = (membership as Record<string, unknown>).id;
  if (typeof id !== "string") return null;
  const canonical = id.toLowerCase();
  return UUID.test(canonical) ? canonical : null;
}

export function parseOrganizationAlias(value: unknown): string | null {
  if (typeof value !== "string") return null;
  const alias = value.trim();
  return ALIAS.test(alias) ? alias : null;
}

export function canonicalOrganizationId(value: unknown): string | null {
  if (typeof value !== "string") return null;
  const canonical = value.trim().toLowerCase();
  return UUID.test(canonical) ? canonical : null;
}

/** Only a relative path inside this app may receive the browser after sign-in. */
export function safeReturnTo(value: unknown, baseURL: string): string {
  if (typeof value !== "string" || value.length === 0 || value.length > 512) return "/";
  if (!value.startsWith("/") || value.startsWith("//") || value.includes("\\")) return "/";
  if (Array.from(value).some((char) => char.charCodeAt(0) < 32 || char.charCodeAt(0) === 127))
    return "/";
  try {
    const resolved = new URL(value, baseURL);
    if (resolved.origin !== new URL(baseURL).origin) return "/";
    return `${resolved.pathname}${resolved.search}${resolved.hash}`;
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

/** Exact r13 checkout-intent scope. The raw identity never reaches the browser. */
export function checkoutScope(
  secret: string,
  issuer: string,
  subject: string,
  organizationId: string,
): string {
  return opaqueScope(secret, ["checkout-intents:v1", issuer, subject, organizationId]);
}

/**
 * Independent opaque session scope for cursor history. Cursor state is keyed by
 * this value plus the current baseHref; no provider claim or session id is exposed.
 */
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

/**
 * Derives the session-bound organization from the account written by the
 * verified callback. Better Auth writes or updates that account before it calls
 * createSession (linked account first), so no later completion handler is needed.
 */
export async function deriveSessionOrganization(
  accounts: readonly OrganizationAccount[],
  decrypt: (token: string) => Promise<string>,
  issuer: string,
): Promise<string | null> {
  const keycloakAccounts = accounts.filter((account) => account.providerId === "keycloak");
  if (keycloakAccounts.length !== 1) return null;
  const token = keycloakAccounts[0]?.accessToken;
  if (!token) return null;
  let plain: string;
  try {
    plain = await decrypt(token);
  } catch {
    return null;
  }
  const claims = readJwtClaims(plain);
  if (!claims || claims.iss !== issuer) return null;
  return singleOrganizationId(claims);
}
