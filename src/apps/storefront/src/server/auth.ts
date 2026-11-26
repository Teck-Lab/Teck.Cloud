import "server-only";

import { type Auth, type BetterAuthOptions, betterAuth } from "better-auth";
import { isAPIError } from "better-auth/api";

import {
  canonicalOrganizationId,
  checkoutScope,
  paginationSessionScope,
  readJwtClaims,
  singleOrganizationId,
} from "./auth-claims";
import { buildAuthOptions } from "./auth-config";
import { type AppKind, type AuthEnv, readAuthEnv } from "./auth-env";

const APP: AppKind = "storefront";

type AuthInstance = Auth<BetterAuthOptions>;

export interface AuthSessionResult {
  readonly session: {
    readonly id: string;
    readonly userId: string;
    readonly commerceOrganizationId?: unknown;
    readonly [key: string]: unknown;
  };
  readonly user: { readonly id: string };
}

export interface LinkedAccount {
  readonly id: string;
  readonly providerId: string;
  readonly accountId: string;
  readonly userId: string;
  readonly accessToken?: string | null;
}

export interface AccessTokenResult {
  readonly accessToken?: string | null;
}

export interface AuthApi {
  getSession(input: { headers: Headers }): Promise<AuthSessionResult | null>;
  listUserAccounts(input: { headers: Headers }): Promise<LinkedAccount[]>;
  getAccessToken(input: {
    headers: Headers;
    body: { accountId: string };
  }): Promise<AccessTokenResult>;
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
}

interface ResolvedAccess {
  readonly sessionId: string;
  readonly userId: string;
  readonly subject: string;
  readonly organizationId: string | null;
  readonly accessToken: string;
}

async function resolveAccess(deps: AccessDeps, headers: Headers): Promise<ResolvedAccess | null> {
  let sessionResult: AuthSessionResult | null;
  try {
    sessionResult = await deps.api.getSession({ headers });
  } catch {
    throw new AuthUnavailableError();
  }
  if (!sessionResult) return null;

  const session = sessionResult.session as typeof sessionResult.session & {
    commerceOrganizationId?: unknown;
  };
  const userId = sessionResult.user?.id;
  if (typeof userId !== "string" || session.userId !== userId || typeof session.id !== "string") {
    return null;
  }
  const organizationId =
    deps.app === "storefront" ? canonicalOrganizationId(session.commerceOrganizationId) : null;
  if (deps.app === "storefront" && !organizationId) return null;

  let accounts: LinkedAccount[];
  try {
    accounts = await deps.api.listUserAccounts({ headers });
  } catch (error) {
    if (isAPIError(error)) return null;
    throw new AuthUnavailableError();
  }
  const linked = accounts.filter((account) => account.providerId === "keycloak");
  const account = linked.length === 1 ? linked[0] : undefined;
  if (!account || account.userId !== userId || typeof account.accountId !== "string") {
    return null;
  }
  if (account.accountId.length === 0) return null;

  let tokenResult: AccessTokenResult;
  try {
    // Only the server-side Better Auth row id selects the account; the fixed
    // shape is the whole selector schema in the pinned release.
    tokenResult = await deps.api.getAccessToken({
      headers,
      body: { accountId: account.id },
    });
  } catch (error) {
    if (isAPIError(error)) return null;
    throw new AuthUnavailableError();
  }
  const accessToken = tokenResult?.accessToken;
  if (!accessToken) return null;

  const claims = readJwtClaims(accessToken);
  if (!claims || claims.iss !== deps.env.issuer) return null;
  if (claims.sub !== account.accountId) return null;
  if (deps.app === "storefront") {
    const tokenOrganizationId = singleOrganizationId(claims);
    if (!tokenOrganizationId || tokenOrganizationId !== organizationId) return null;
  }

  return {
    sessionId: session.id,
    userId,
    subject: account.accountId,
    organizationId,
    accessToken,
  };
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
      let result: AuthSessionResult | null;
      try {
        result = await deps.api.getSession({ headers });
      } catch {
        throw new AuthUnavailableError();
      }
      if (!result) return null;
      const session = result.session as typeof result.session & {
        commerceOrganizationId?: unknown;
      };
      const userId = result.user?.id;
      if (
        typeof userId !== "string" ||
        session.userId !== userId ||
        typeof session.id !== "string"
      ) {
        return null;
      }
      const organizationId =
        deps.app === "storefront" ? canonicalOrganizationId(session.commerceOrganizationId) : null;
      if (deps.app === "storefront" && !organizationId) return null;
      return { sessionId: session.id, userId, organizationId };
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
      if (!access?.organizationId) return null;
      return {
        accessToken: access.accessToken,
        scope: checkoutScope(
          deps.env.secret,
          deps.env.issuer,
          access.subject,
          access.organizationId,
        ),
      };
    },
  };
}

function defaultAccess(): AuthAccess {
  return createAuthAccess({
    api: getAuth().api,
    env: readAuthEnv(process.env, APP),
    app: APP,
  });
}

/** Null means no eligible session or linked account; throws mean unavailable. */
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
