import "server-only";

import { type Auth, type BetterAuthOptions, betterAuth } from "better-auth";
import { isAPIError } from "better-auth/api";
import { decryptOAuthToken, setTokenUtil } from "better-auth/oauth2";

import {
  canonicalOrganizationId,
  checkoutScope,
  paginationSessionScope,
  readJwtClaims,
} from "./auth-claims";
import { buildAuthOptions } from "./auth-config";
import { type AppKind, type AuthEnv, readAuthEnv } from "./auth-env";
import {
  buildRefreshedGrant,
  createSessionGrantStore,
  type GrantAdapter,
  grantAccessTokenIsFresh,
  grantAccessTokenMatches,
  grantMatchesSession,
  type RefreshedTokens,
  type SessionGrant,
  type SessionGrantStore,
  type StoredGrant,
  serializeSessionGrant,
} from "./auth-grant";

const APP: AppKind = "storefront";

type AuthInstance = Auth<BetterAuthOptions>;

export interface AuthSessionResult {
  readonly session: {
    readonly id: string;
    readonly userId: string;
    readonly expiresAt?: unknown;
    readonly commerceOrganizationId?: unknown;
    readonly [key: string]: unknown;
  };
  readonly user: { readonly id: string };
}

export interface AuthApi {
  getSession(input: { headers: Headers }): Promise<AuthSessionResult | null>;
}

export class AuthUnavailableError extends Error {
  constructor() {
    super("Authentication is temporarily unavailable.");
    this.name = "AuthUnavailableError";
  }
}

export interface AuthSessionSummary {
  readonly sessionId: string;
  readonly userId: string;
  readonly organizationId: string | null;
}

/** Encryption and refresh of this app's own session grant. */
export interface GrantTokenService {
  encrypt(token: string): Promise<string>;
  decrypt(token: string): Promise<string>;
  refresh(refreshToken: string): Promise<RefreshedTokens | null>;
}

let authInstance: AuthInstance | null = null;

/**
 * Lazy process-scoped instance. Importing a page or route during `next build`
 * never reads configuration or opens the auth database.
 */
export function getAuth(): AuthInstance {
  if (!authInstance) {
    authInstance = betterAuth(buildAuthOptions(readAuthEnv(process.env, APP), APP));
  }
  return authInstance;
}

interface AccessDeps {
  readonly api: AuthApi;
  readonly env: AuthEnv;
  readonly app: AppKind;
  readonly tokens: GrantTokenService;
  readonly grants: SessionGrantStore;
  readonly now?: () => number;
}

interface BoundSession {
  readonly sessionId: string;
  readonly userId: string;
  readonly organizationId: string | null;
  readonly grant: SessionGrant;
  readonly expiresAt: unknown;
}

interface ResolvedAccess {
  readonly grant: SessionGrant;
  readonly accessToken: string;
}

async function readSession(deps: AccessDeps, headers: Headers): Promise<AuthSessionResult | null> {
  try {
    return await deps.api.getSession({ headers });
  } catch {
    throw new AuthUnavailableError();
  }
}

/**
 * A session is authenticated only when its own `verification` row still binds
 * that exact session, user, issuer and organization.
 */
async function boundSession(
  deps: AccessDeps,
  sessionResult: AuthSessionResult | null,
): Promise<BoundSession | null> {
  if (!sessionResult) return null;
  const session = sessionResult.session;
  const userId = sessionResult.user?.id;
  if (typeof userId !== "string" || session.userId !== userId || typeof session.id !== "string") {
    return null;
  }
  const organizationId =
    deps.app === "storefront" ? canonicalOrganizationId(session.commerceOrganizationId) : null;
  if (deps.app === "storefront" && !organizationId) return null;

  let stored: StoredGrant | null;
  try {
    stored = await deps.grants.read({ sessionId: session.id, userId });
  } catch {
    throw new AuthUnavailableError();
  }
  if (!stored) return null;
  const grant = stored.grant;
  if (
    !grantMatchesSession({
      grant,
      session: {
        sessionId: session.id,
        userId,
        commerceOrganizationId: session.commerceOrganizationId,
      },
      env: deps.env,
      app: deps.app,
    })
  ) {
    return null;
  }
  return {
    sessionId: session.id,
    userId,
    organizationId,
    grant,
    expiresAt: session.expiresAt,
  };
}

async function decryptAccessToken(
  deps: AccessDeps,
  grant: SessionGrant,
): Promise<{ accessToken: string; claims: Record<string, unknown> } | null> {
  let accessToken: string;
  try {
    accessToken = await deps.tokens.decrypt(grant.accessToken);
  } catch {
    return null;
  }
  const claims = readJwtClaims(accessToken);
  if (!claims || !grantAccessTokenMatches({ grant, env: deps.env, app: deps.app, claims }))
    return null;
  return { accessToken, claims };
}

/**
 * Refreshes only this session's own grant. The compare-and-swap requires the
 * exact row id and the exact previously verified serialized value, so
 * concurrent refreshes cannot overwrite each other; a loser re-reads the
 * winner's committed row.
 */
async function refreshGrant(
  deps: AccessDeps,
  session: BoundSession,
  current: SessionGrant,
): Promise<SessionGrant | null> {
  const encryptedRefreshToken = current.refreshToken;
  if (!encryptedRefreshToken) return null;

  let refreshToken: string;
  try {
    refreshToken = await deps.tokens.decrypt(encryptedRefreshToken);
  } catch {
    return null;
  }

  let tokens: RefreshedTokens | null;
  try {
    tokens = await deps.tokens.refresh(refreshToken);
  } catch (error) {
    if (isAPIError(error)) return null;
    throw new AuthUnavailableError();
  }
  if (!tokens) return null;

  const next = await buildRefreshedGrant({
    previous: current,
    tokens,
    env: deps.env,
    app: deps.app,
    encrypt: (token) => deps.tokens.encrypt(token),
  });
  if (!next) return null;

  let rotated: boolean;
  try {
    rotated = await deps.grants.rotate({
      previous: current,
      next,
      sessionExpiresAt: session.expiresAt,
    });
  } catch {
    throw new AuthUnavailableError();
  }
  if (rotated) return next;

  let reread: StoredGrant | null;
  try {
    reread = await deps.grants.read({ sessionId: session.sessionId, userId: session.userId });
  } catch {
    throw new AuthUnavailableError();
  }
  if (!reread || serializeSessionGrant(reread.grant) === serializeSessionGrant(current)) {
    return null;
  }
  const changed = reread.grant;
  if (
    !grantMatchesSession({
      grant: changed,
      session: {
        sessionId: session.sessionId,
        userId: session.userId,
        commerceOrganizationId: session.organizationId,
      },
      env: deps.env,
      app: deps.app,
    })
  ) {
    return null;
  }
  return changed;
}

async function resolveAccess(deps: AccessDeps, headers: Headers): Promise<ResolvedAccess | null> {
  const bound = await boundSession(deps, await readSession(deps, headers));
  if (!bound) return null;

  const now = (deps.now ?? Date.now)();
  let current = bound.grant;
  let access = await decryptAccessToken(deps, current);
  if (!access) return null;

  if (!grantAccessTokenIsFresh({ grant: current, claims: access.claims, now })) {
    const refreshed = await refreshGrant(deps, bound, current);
    if (!refreshed) return null;
    current = refreshed;
    access = await decryptAccessToken(deps, current);
    if (!access) return null;
    if (!grantAccessTokenIsFresh({ grant: current, claims: access.claims, now })) return null;
  }

  return { grant: current, accessToken: access.accessToken };
}

export interface AuthAccess {
  getGatewayAccessToken(headers: Headers): Promise<string | null>;
  appSessionScope(headers: Headers): Promise<string | null>;
  getAuthSession(headers: Headers): Promise<AuthSessionSummary | null>;
  getCommerceIdentity(headers: Headers): Promise<{ accessToken: string; scope: string } | null>;
}

export function createAuthAccess(deps: AccessDeps): AuthAccess {
  return {
    async getGatewayAccessToken(headers) {
      return (await resolveAccess(deps, headers))?.accessToken ?? null;
    },

    async getAuthSession(headers) {
      const bound = await boundSession(deps, await readSession(deps, headers));
      if (!bound) return null;
      return {
        sessionId: bound.sessionId,
        userId: bound.userId,
        organizationId: bound.organizationId,
      };
    },

    async appSessionScope(headers) {
      const session = await this.getAuthSession(headers);
      if (!session) return null;
      return paginationSessionScope(
        deps.env.secret,
        deps.app,
        deps.env.issuer,
        session.userId,
        session.sessionId,
        session.organizationId,
      );
    },

    async getCommerceIdentity(headers) {
      const access = await resolveAccess(deps, headers);
      if (!access?.grant.organizationId) return null;
      return {
        accessToken: access.accessToken,
        scope: checkoutScope(
          deps.env.secret,
          deps.env.issuer,
          access.grant.subject,
          access.grant.organizationId,
        ),
      };
    },
  };
}

function createTokenService(auth: AuthInstance): GrantTokenService {
  return {
    async encrypt(token) {
      const encrypted = await setTokenUtil(token, await auth.$context);
      if (typeof encrypted !== "string") throw new Error("Token encryption is unavailable.");
      return encrypted;
    },
    async decrypt(token) {
      return await decryptOAuthToken(token, await auth.$context);
    },
    async refresh(refreshToken) {
      const context = await auth.$context;
      const provider = context.socialProviders.find((candidate) => candidate.id === "keycloak");
      if (!provider?.refreshAccessToken) return null;
      return await provider.refreshAccessToken(refreshToken);
    },
  };
}

function createGrantStore(auth: AuthInstance, env: AuthEnv, app: AppKind): SessionGrantStore {
  const store = async (): Promise<SessionGrantStore> =>
    createSessionGrantStore((await auth.$context).adapter as unknown as GrantAdapter, env, app);
  return {
    async create(input) {
      return await (await store()).create(input);
    },
    async read(input) {
      return await (await store()).read(input);
    },
    async rotate(input) {
      return await (await store()).rotate(input);
    },
  };
}

function defaultAccess(): AuthAccess {
  const auth = getAuth();
  const env = readAuthEnv(process.env, APP);
  return createAuthAccess({
    api: auth.api as unknown as AuthApi,
    env,
    app: APP,
    tokens: createTokenService(auth),
    grants: createGrantStore(auth, env, APP),
  });
}

/** Null means no verified session grant; throws mean unavailable. */
export function getGatewayAccessToken(headers: Headers): Promise<string | null> {
  return defaultAccess().getGatewayAccessToken(headers);
}

/** Opaque, session-bound scope for pagination cursor history; never raw identity. */
export function appSessionScope(headers: Headers): Promise<string | null> {
  return defaultAccess().appSessionScope(headers);
}

/** Server-only session summary for protected page gating. */
export function getAuthSession(headers: Headers): Promise<AuthSessionSummary | null> {
  return defaultAccess().getAuthSession(headers);
}

/** Storefront linked-account identity plus the exact checkout-intent scope. */
export function getCommerceIdentity(
  headers: Headers,
): Promise<{ accessToken: string; scope: string } | null> {
  return defaultAccess().getCommerceIdentity(headers);
}
