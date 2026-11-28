import { createHmac } from "node:crypto";

import { canonicalOrganizationId, readJwtClaims, singleOrganizationId } from "./auth-claims";
import type { AppKind, AuthEnv } from "./auth-env";

/**
 * Session-owned Keycloak grant backed by the auth-only `verification` table.
 *
 * Better Auth 1.7.28 verifies the authorization-code exchange and the ID token
 * before it writes the OAuth account (`dist/oauth2/callback.mjs` and
 * `dist/oauth2/verify-id-token.mjs`). The account row is shared by every
 * sign-in of the same provider subject and is overwritten on repeat sign-in,
 * so it cannot identify one session's grant.
 *
 * The account hook of the callback request captures the tokens returned by
 * that verified exchange into a request-local WeakMap keyed by the Better Auth
 * endpoint context. The session-create hook, which has the new session id,
 * binds the captured value to that exact session and writes an encrypted
 * `verification` row. Protected requests re-read only that row after Better
 * Auth resolves the same session. No request path rereads the shared account
 * token.
 *
 * Only the Better Auth-encrypted access and refresh tokens are persisted. The
 * ID token is used transiently for issuer/audience/subject checks and is
 * stripped before the account row is written.
 */
export const GRANT_NAMESPACE = "teck.keycloak-grant.v1";
export const SESSION_GRANT_VERSION = 1;

const MAX_GRANT_LENGTH = 32_768;
const MAX_TOKEN_LENGTH = 16_384;
const MAX_SUBJECT_LENGTH = 512;
const MAX_ISSUER_LENGTH = 2_048;
const MAX_GRANT_LIFETIME_MS = 30 * 24 * 60 * 60 * 1_000;
const FRESHNESS_SKEW_MS = 30_000;
const ENVELOPE = /^\$ba\$\d+\$[0-9a-f]+$/i;
const LEGACY_HEX = /^(?:[0-9a-f]{2})+$/i;

export interface SessionGrant {
  readonly version: 1;
  readonly sessionId: string;
  readonly userId: string;
  readonly issuer: string;
  readonly subject: string;
  readonly organizationId: string | null;
  readonly accessToken: string;
  readonly refreshToken: string | null;
  readonly accessTokenExpiresAt: string | null;
  readonly refreshTokenExpiresAt: string | null;
}

export interface CapturedGrant {
  readonly version: 1;
  readonly userId: string;
  readonly issuer: string;
  readonly subject: string;
  readonly organizationId: string | null;
  readonly accessToken: string;
  readonly refreshToken: string | null;
  readonly accessTokenExpiresAt: string | null;
  readonly refreshTokenExpiresAt: string | null;
}

export interface RefreshedTokens {
  readonly accessToken?: unknown;
  readonly refreshToken?: unknown;
  readonly accessTokenExpiresAt?: unknown;
  readonly refreshTokenExpiresAt?: unknown;
}

export interface StoredGrant {
  readonly id: string;
  readonly value: string;
  readonly expiresAt: string;
  readonly grant: SessionGrant;
}

export interface GrantAdapter {
  create(input: {
    model: "verification";
    data: Record<string, unknown>;
    forceAllowId?: boolean;
  }): Promise<unknown>;
  findOne(input: {
    model: "verification";
    where: { field: string; value: unknown; operator?: string }[];
  }): Promise<unknown>;
  update(input: {
    model: "verification";
    where: { field: string; value: unknown; operator?: string }[];
    update: Record<string, unknown>;
  }): Promise<unknown>;
}

export interface SessionGrantStore {
  create(input: { grant: SessionGrant; sessionExpiresAt: unknown }): Promise<boolean>;
  read(input: { sessionId: string; userId: string }): Promise<StoredGrant | null>;
  rotate(input: {
    previous: SessionGrant;
    next: SessionGrant;
    sessionExpiresAt: unknown;
  }): Promise<boolean>;
}

/** Better Auth's `isLikelyEncrypted` envelope plus its legacy bare-hex form. */
export function isEncryptedOAuthToken(value: unknown): value is string {
  if (typeof value !== "string") return false;
  if (value.length === 0 || value.length > MAX_TOKEN_LENGTH) return false;
  return ENVELOPE.test(value) || LEGACY_HEX.test(value);
}

function isoDate(value: unknown): string | null {
  if (value instanceof Date) {
    return Number.isNaN(value.getTime()) ? null : value.toISOString();
  }
  if (typeof value !== "string") return null;
  const timestamp = Date.parse(value);
  if (!Number.isFinite(timestamp)) return null;
  return new Date(timestamp).toISOString();
}

function tokenExpiry(value: unknown, claims: Record<string, unknown> | null): string | null {
  const explicit = isoDate(value);
  if (explicit) return explicit;
  const seconds = claims?.exp;
  if (typeof seconds !== "number" || !Number.isFinite(seconds)) return null;
  return new Date(seconds * 1_000).toISOString();
}

function boundedString(value: unknown, max: number): string | null {
  if (typeof value !== "string" || value.length === 0 || value.length > max) return null;
  return value;
}

function audienceMatches(value: unknown, clientId: string): boolean {
  if (value === clientId) return true;
  return Array.isArray(value) && value.includes(clientId);
}

export interface CapturedAccount {
  readonly providerId: string;
  readonly accountId?: string | null | undefined;
  readonly userId?: string | null | undefined;
  readonly accessToken?: string | null | undefined;
  readonly refreshToken?: string | null | undefined;
  readonly idToken?: string | null | undefined;
  readonly accessTokenExpiresAt?: unknown;
  readonly refreshTokenExpiresAt?: unknown;
}

export interface CaptureGrantInput {
  readonly account: CapturedAccount;
  readonly env: AuthEnv;
  readonly app: AppKind;
  readonly decrypt: (token: string) => Promise<string>;
}

/**
 * Narrows the tokens of one verified callback into a grant. The ID token must
 * carry the configured issuer and client audience, and agree with the provider
 * subject. The access token must be the encrypted token exchanged by that
 * callback, with the same issuer and subject; Storefront also requires the
 * single organization claim. The ID token is never returned for persistence.
 */
export async function captureVerifiedGrant(
  input: CaptureGrantInput,
): Promise<CapturedGrant | null> {
  const { account, env, app } = input;
  if (account.providerId !== "keycloak") return null;

  const idToken = account.idToken;
  if (typeof idToken !== "string" || idToken.length === 0) return null;
  const idClaims = readJwtClaims(idToken);
  if (!idClaims || idClaims.iss !== env.issuer) return null;
  if (!audienceMatches(idClaims.aud, env.clientId)) return null;
  const subject = boundedString(idClaims.sub, MAX_SUBJECT_LENGTH);
  if (!subject) return null;
  const accountId = account.accountId;
  if (accountId !== undefined && accountId !== null && accountId !== subject) return null;

  const accessToken = account.accessToken;
  if (!isEncryptedOAuthToken(accessToken)) return null;
  let plainAccessToken: string;
  try {
    plainAccessToken = await input.decrypt(accessToken);
  } catch {
    return null;
  }
  const accessClaims = readJwtClaims(plainAccessToken);
  if (!accessClaims || accessClaims.iss !== env.issuer || accessClaims.sub !== subject) {
    return null;
  }

  let organizationId: string | null = null;
  if (app === "storefront") {
    organizationId = singleOrganizationId(accessClaims);
    if (!organizationId) return null;
  }

  const refreshToken = account.refreshToken;
  if (refreshToken !== undefined && refreshToken !== null && !isEncryptedOAuthToken(refreshToken)) {
    return null;
  }

  return {
    version: SESSION_GRANT_VERSION,
    userId: boundedString(account.userId, MAX_SUBJECT_LENGTH) ?? "",
    issuer: env.issuer,
    subject,
    organizationId,
    accessToken,
    refreshToken: refreshToken ?? null,
    accessTokenExpiresAt: tokenExpiry(account.accessTokenExpiresAt, accessClaims),
    refreshTokenExpiresAt: isoDate(account.refreshTokenExpiresAt),
  };
}

export interface SessionBinding {
  readonly sessionId: unknown;
  readonly userId: unknown;
  readonly commerceOrganizationId?: unknown;
}

/** Binds a callback-local captured grant to the exact session it created. */
export function bindCapturedGrant(input: {
  captured: CapturedGrant;
  session: SessionBinding;
  env: AuthEnv;
  app: AppKind;
}): SessionGrant | null {
  const { captured, env, app } = input;
  if (captured.version !== SESSION_GRANT_VERSION) return null;
  if (captured.issuer !== env.issuer) return null;
  const sessionId = boundedString(input.session.sessionId, MAX_SUBJECT_LENGTH);
  const userId = boundedString(input.session.userId, MAX_SUBJECT_LENGTH);
  if (!sessionId || !userId) return null;
  if (!boundedString(captured.subject, MAX_SUBJECT_LENGTH)) return null;
  if (captured.userId.length > 0 && captured.userId !== userId) return null;
  if (!isEncryptedOAuthToken(captured.accessToken)) return null;
  if (captured.refreshToken !== null && !isEncryptedOAuthToken(captured.refreshToken)) {
    return null;
  }
  if (app === "storefront") {
    const sessionOrganization = canonicalOrganizationId(input.session.commerceOrganizationId);
    if (!sessionOrganization || sessionOrganization !== captured.organizationId) return null;
  } else if (captured.organizationId !== null) {
    return null;
  }
  return { ...captured, sessionId, userId };
}

/** Strict parse of the `verification.value` payload; anything odd is unauthenticated. */
export function parseSessionGrant(value: unknown): SessionGrant | null {
  if (typeof value !== "string" || value.length > MAX_GRANT_LENGTH) return null;
  if (!value.startsWith("{")) return null;
  let parsed: unknown;
  try {
    parsed = JSON.parse(value);
  } catch {
    return null;
  }
  if (parsed === null || typeof parsed !== "object" || Array.isArray(parsed)) return null;
  const data = parsed as Record<string, unknown>;
  if (data.version !== SESSION_GRANT_VERSION) return null;
  const sessionId = boundedString(data.sessionId, MAX_SUBJECT_LENGTH);
  const userId = boundedString(data.userId, MAX_SUBJECT_LENGTH);
  const issuer = boundedString(data.issuer, MAX_ISSUER_LENGTH);
  const subject = boundedString(data.subject, MAX_SUBJECT_LENGTH);
  if (!sessionId || !userId || !issuer || !subject) return null;
  if (!isEncryptedOAuthToken(data.accessToken)) return null;
  const accessToken = data.accessToken;
  const refreshToken: string | null =
    data.refreshToken === null
      ? null
      : isEncryptedOAuthToken(data.refreshToken)
        ? data.refreshToken
        : null;
  if (data.refreshToken !== null && refreshToken === null) return null;
  const organizationId =
    data.organizationId === null ? null : canonicalOrganizationId(data.organizationId);
  if (data.organizationId !== null && !organizationId) return null;
  const accessTokenExpiresAt =
    data.accessTokenExpiresAt === null ? null : isoDate(data.accessTokenExpiresAt);
  if (data.accessTokenExpiresAt !== null && !accessTokenExpiresAt) return null;
  const refreshTokenExpiresAt =
    data.refreshTokenExpiresAt === null ? null : isoDate(data.refreshTokenExpiresAt);
  if (data.refreshTokenExpiresAt !== null && !refreshTokenExpiresAt) return null;
  return {
    version: SESSION_GRANT_VERSION,
    sessionId,
    userId,
    issuer,
    subject,
    organizationId,
    accessToken,
    refreshToken,
    accessTokenExpiresAt,
    refreshTokenExpiresAt,
  };
}

/** Stable serialization used for the compare-and-swap rotation. */
export function serializeSessionGrant(grant: SessionGrant): string {
  return JSON.stringify({
    version: grant.version,
    sessionId: grant.sessionId,
    userId: grant.userId,
    issuer: grant.issuer,
    subject: grant.subject,
    organizationId: grant.organizationId,
    accessToken: grant.accessToken,
    refreshToken: grant.refreshToken,
    accessTokenExpiresAt: grant.accessTokenExpiresAt,
    refreshTokenExpiresAt: grant.refreshTokenExpiresAt,
  });
}

export function grantMatchesSession(input: {
  grant: SessionGrant | null;
  session: SessionBinding & { readonly userId: unknown };
  env: AuthEnv;
  app: AppKind;
}): boolean {
  const { grant, session, env, app } = input;
  if (!grant) return false;
  if (grant.version !== SESSION_GRANT_VERSION) return false;
  if (grant.issuer !== env.issuer) return false;
  if (grant.sessionId !== session.sessionId) return false;
  if (grant.userId !== session.userId) return false;
  if (!boundedString(grant.subject, MAX_SUBJECT_LENGTH)) return false;
  if (!isEncryptedOAuthToken(grant.accessToken)) return false;
  if (grant.refreshToken !== null && !isEncryptedOAuthToken(grant.refreshToken)) return false;
  if (app === "storefront") {
    const organizationId = canonicalOrganizationId(session.commerceOrganizationId);
    return organizationId !== null && organizationId === grant.organizationId;
  }
  return grant.organizationId === null;
}

export function grantAccessTokenMatches(input: {
  grant: SessionGrant;
  env: AuthEnv;
  app: AppKind;
  claims: Record<string, unknown> | null;
}): boolean {
  const { grant, env, app, claims } = input;
  if (!claims) return false;
  if (claims.iss !== env.issuer) return false;
  if (claims.sub !== grant.subject) return false;
  if (app === "storefront") {
    return singleOrganizationId(claims) === grant.organizationId;
  }
  return true;
}

export function grantAccessTokenIsFresh(input: {
  grant: SessionGrant;
  claims: Record<string, unknown> | null;
  now: number;
}): boolean {
  const expiries: number[] = [];
  if (input.grant.accessTokenExpiresAt) {
    const parsed = Date.parse(input.grant.accessTokenExpiresAt);
    if (Number.isFinite(parsed)) expiries.push(parsed);
  }
  const seconds = input.claims?.exp;
  if (typeof seconds === "number" && Number.isFinite(seconds)) expiries.push(seconds * 1_000);
  if (expiries.length === 0) return false;
  return Math.min(...expiries) - input.now > FRESHNESS_SKEW_MS;
}

/**
 * Validates the refreshed token exchange against the session's own previous
 * grant and re-encrypts only access and refresh tokens. Any ID token returned
 * by the refresh endpoint is ignored and never persisted.
 */
export async function buildRefreshedGrant(input: {
  previous: SessionGrant;
  tokens: RefreshedTokens;
  env: AuthEnv;
  app: AppKind;
  encrypt: (token: string) => Promise<string>;
}): Promise<SessionGrant | null> {
  const plainAccessToken = input.tokens.accessToken;
  if (
    typeof plainAccessToken !== "string" ||
    plainAccessToken.length === 0 ||
    plainAccessToken.length > MAX_TOKEN_LENGTH
  ) {
    return null;
  }
  const claims = readJwtClaims(plainAccessToken);
  if (
    !grantAccessTokenMatches({
      grant: input.previous,
      env: input.env,
      app: input.app,
      claims,
    })
  ) {
    return null;
  }
  let accessToken: string;
  try {
    accessToken = await input.encrypt(plainAccessToken);
  } catch {
    return null;
  }
  if (!isEncryptedOAuthToken(accessToken)) return null;

  let refreshToken = input.previous.refreshToken;
  const nextRefreshToken = input.tokens.refreshToken;
  if (typeof nextRefreshToken === "string" && nextRefreshToken.length > 0) {
    let encryptedRefreshToken: string;
    try {
      encryptedRefreshToken = await input.encrypt(nextRefreshToken);
    } catch {
      return null;
    }
    if (!isEncryptedOAuthToken(encryptedRefreshToken)) return null;
    refreshToken = encryptedRefreshToken;
  }

  return {
    version: SESSION_GRANT_VERSION,
    sessionId: input.previous.sessionId,
    userId: input.previous.userId,
    issuer: input.previous.issuer,
    subject: input.previous.subject,
    organizationId: input.previous.organizationId,
    accessToken,
    refreshToken,
    accessTokenExpiresAt: tokenExpiry(input.tokens.accessTokenExpiresAt, claims),
    refreshTokenExpiresAt:
      isoDate(input.tokens.refreshTokenExpiresAt) ?? input.previous.refreshTokenExpiresAt,
  };
}

/** Namespaced opaque row id derived from the exact session and its owner. */
export function grantRowId(input: {
  secret: string;
  app: AppKind;
  issuer: string;
  sessionId: string;
  userId: string;
}): string {
  const digest = createHmac("sha256", input.secret)
    .update(
      JSON.stringify([GRANT_NAMESPACE, input.app, input.issuer, input.sessionId, input.userId]),
    )
    .digest("base64url");
  return `${GRANT_NAMESPACE}:${input.app}:${digest}`;
}

export function grantRowIdentifier(app: AppKind): string {
  return `${GRANT_NAMESPACE}:${app}`;
}

/**
 * The row must not outlive its session, the protocol's 30-day ceiling or a
 * shorter refresh-token lifetime.
 */
export function grantExpiresAt(input: {
  grant: SessionGrant;
  sessionExpiresAt: unknown;
  now: number;
}): Date | null {
  const sessionExpiry = isoDate(input.sessionExpiresAt);
  if (!sessionExpiry) return null;
  const sessionExpiresAt = Date.parse(sessionExpiry);
  if (!Number.isFinite(sessionExpiresAt) || sessionExpiresAt <= input.now) return null;
  let expires = Math.min(sessionExpiresAt, input.now + MAX_GRANT_LIFETIME_MS);
  if (input.grant.refreshTokenExpiresAt) {
    const refreshExpires = Date.parse(input.grant.refreshTokenExpiresAt);
    if (!Number.isFinite(refreshExpires)) return null;
    expires = Math.min(expires, refreshExpires);
  }
  return expires > input.now ? new Date(expires) : null;
}

function storedExpiry(row: Record<string, unknown>): number | null {
  const value = isoDate(row.expiresAt);
  if (!value) return null;
  const timestamp = Date.parse(value);
  return Number.isFinite(timestamp) ? timestamp : null;
}

export function createSessionGrantStore(
  adapter: GrantAdapter,
  env: AuthEnv,
  app: AppKind,
  now: () => number = Date.now,
): SessionGrantStore {
  const identifier = grantRowIdentifier(app);
  const rowId = (grant: SessionGrant): string =>
    grantRowId({
      secret: env.secret,
      app,
      issuer: env.issuer,
      sessionId: grant.sessionId,
      userId: grant.userId,
    });

  return {
    async create({ grant, sessionExpiresAt }) {
      const expiresAt = grantExpiresAt({ grant, sessionExpiresAt, now: now() });
      if (!expiresAt) return false;
      const timestamp = new Date(now());
      try {
        await adapter.create({
          model: "verification",
          forceAllowId: true,
          data: {
            id: rowId(grant),
            identifier,
            value: serializeSessionGrant(grant),
            expiresAt,
            createdAt: timestamp,
            updatedAt: timestamp,
          },
        });
        return true;
      } catch {
        return false;
      }
    },

    async read({ sessionId, userId }) {
      const id = grantRowId({
        secret: env.secret,
        app,
        issuer: env.issuer,
        sessionId,
        userId,
      });
      let row: unknown;
      try {
        row = await adapter.findOne({ model: "verification", where: [{ field: "id", value: id }] });
      } catch {
        throw new Error("The session grant store is unavailable.");
      }
      if (row === null || typeof row !== "object" || Array.isArray(row)) return null;
      const record = row as Record<string, unknown>;
      if (record.id !== id || record.identifier !== identifier) return null;
      const expiresAt = storedExpiry(record);
      if (expiresAt === null || expiresAt <= now()) return null;
      const grant = parseSessionGrant(record.value);
      if (!grant || grant.sessionId !== sessionId || grant.userId !== userId) return null;
      return {
        id,
        value: record.value as string,
        expiresAt: new Date(expiresAt).toISOString(),
        grant,
      };
    },

    async rotate({ previous, next, sessionExpiresAt }) {
      const expiresAt = grantExpiresAt({ grant: next, sessionExpiresAt, now: now() });
      if (!expiresAt) return false;
      let updated: unknown;
      try {
        updated = await adapter.update({
          model: "verification",
          where: [
            { field: "id", value: rowId(previous) },
            { field: "value", value: serializeSessionGrant(previous) },
            { field: "expiresAt", value: new Date(now()), operator: "gt" },
          ],
          update: {
            value: serializeSessionGrant(next),
            expiresAt,
            updatedAt: new Date(now()),
          },
        });
      } catch {
        throw new Error("The session grant store is unavailable.");
      }
      return Boolean(updated);
    },
  };
}

const requestGrants = new WeakMap<object, CapturedGrant>();

/**
 * Request-local ownership. The endpoint context object is per HTTP request and
 * is the same object both database hooks receive, so concurrent callbacks never
 * observe each other's grant.
 */
export function rememberRequestGrant(
  context: object | null | undefined,
  grant: CapturedGrant,
): void {
  if (context === null || typeof context !== "object") return;
  requestGrants.set(context, grant);
}

export function readRequestGrant(context: object | null | undefined): CapturedGrant | null {
  if (context === null || typeof context !== "object") return null;
  return requestGrants.get(context) ?? null;
}
