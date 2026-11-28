import type { Account, BetterAuthOptions, GenericEndpointContext, Session } from "better-auth";
import { nextCookies } from "better-auth/next-js";
import { decryptOAuthToken } from "better-auth/oauth2";
import { type GenericOAuthConfig, genericOAuth, keycloak } from "better-auth/plugins/generic-oauth";
import { Pool } from "pg";
import { type AppKind, AuthConfigError, type AuthEnv } from "./auth-env";
import {
  bindCapturedGrant,
  type CapturedAccount,
  captureVerifiedGrant,
  createSessionGrantStore,
  type GrantAdapter,
  readRequestGrant,
  rememberRequestGrant,
} from "./auth-grant";

/** Both apps request these; Storefront adds exactly one organization hint. */
export const BASE_AUTH_SCOPES = ["openid", "profile", "email"] as const;

interface VerifiedProviderIdToken {
  readonly jwks?: unknown;
  readonly issuer?: string | string[];
  readonly audience?: string | string[];
  readonly allowOpaqueToken?: boolean;
}

interface VerifiedProvider {
  readonly id: string;
  readonly issuer?: string;
  readonly requiresIdTokenNonce?: boolean;
  readonly idToken?: unknown;
}

export interface VerifiedProviderSource {
  readonly $context: Promise<{ readonly socialProviders: readonly VerifiedProvider[] }>;
}

function audienceIncludes(value: unknown, clientId: string): boolean {
  if (typeof value === "string") return value === clientId;
  return Array.isArray(value) && value.includes(clientId);
}

/**
 * Fail closed unless the registered provider is the Keycloak issuer this app
 * configured, verifies ID-token signatures against its discovered JWKS, and
 * binds the ID token to the authorization request nonce. Better Auth 1.7.28
 * builds `provider.issuer` from the discovery document
 * (`plugins/generic-oauth/index.mjs`), so a redirected or substituted discovery
 * document must never become the trust anchor.
 */
export async function assertVerifiedKeycloakProvider(
  source: VerifiedProviderSource,
  env: AuthEnv,
): Promise<void> {
  let providers: readonly VerifiedProvider[];
  try {
    providers = (await source.$context).socialProviders;
  } catch {
    throw new AuthConfigError("Keycloak discovery is unavailable.");
  }
  const provider = providers.find((candidate) => candidate.id === "keycloak");
  if (!provider) {
    throw new AuthConfigError("Keycloak discovery did not register a usable provider.");
  }
  if (provider.issuer !== env.issuer) {
    throw new AuthConfigError("Keycloak discovery issuer does not match the configured issuer.");
  }
  if (provider.requiresIdTokenNonce !== true) {
    throw new AuthConfigError("Keycloak ID tokens must be bound to the request nonce.");
  }
  const rawIdToken = provider.idToken;
  if (rawIdToken === null || typeof rawIdToken !== "object") {
    throw new AuthConfigError("Keycloak discovery did not provide JWKS verification.");
  }
  const idToken = rawIdToken as VerifiedProviderIdToken;
  if (!("jwks" in idToken) || !idToken.jwks) {
    throw new AuthConfigError("Keycloak discovery did not provide JWKS verification.");
  }
  if (idToken.allowOpaqueToken === true) {
    throw new AuthConfigError("Opaque Keycloak ID tokens are not accepted.");
  }
  if (idToken.issuer !== env.issuer) {
    throw new AuthConfigError("Keycloak ID-token issuer does not match the configured issuer.");
  }
  if (!audienceIncludes(idToken.audience, env.clientId)) {
    throw new AuthConfigError("Keycloak ID-token audience does not include this client.");
  }
}

function capturedAccount(account: Partial<Account>): CapturedAccount {
  return {
    providerId: account.providerId ?? "",
    accountId: account.accountId,
    userId: account.userId,
    accessToken: account.accessToken,
    refreshToken: account.refreshToken,
    idToken: account.idToken,
    accessTokenExpiresAt: account.accessTokenExpiresAt,
    refreshTokenExpiresAt: account.refreshTokenExpiresAt,
  };
}

/**
 * Captures the grant of this callback request. A failed verification throws,
 * rolling back account creation or refusing the account update, so a callback
 * whose tokens cannot be verified never reaches session creation. The plaintext
 * ID token is used transiently and stripped.
 */
async function captureAccountGrant(
  env: AuthEnv,
  app: AppKind,
  account: Partial<Account>,
  context: GenericEndpointContext | null,
): Promise<boolean | { data: Partial<Account> } | undefined> {
  if (account.providerId !== "keycloak") return;
  if (!context) throw new AuthConfigError("Account creation has no request context.");
  const captured = await captureVerifiedGrant({
    account: capturedAccount(account),
    env,
    app,
    decrypt: async (token) => await decryptOAuthToken(token, context.context),
  });
  if (!captured) throw new AuthConfigError("The callback grant could not be verified.");
  rememberRequestGrant(context, captured);
  return { data: { ...account, idToken: null } };
}

/**
 * Writes the captured grant to its own encrypted `verification` row after the
 * session id exists. A failure throws, so Better Auth does not set a session
 * cookie for a session that has no grant.
 */
async function storeSessionGrant(input: {
  env: AuthEnv;
  app: AppKind;
  session: Session & Record<string, unknown>;
  context: GenericEndpointContext | null;
}): Promise<void> {
  const { env, app, session, context } = input;
  if (!context) throw new AuthConfigError("Session creation has no request context.");
  const captured = readRequestGrant(context);
  const grant = captured
    ? bindCapturedGrant({
        captured,
        session: {
          sessionId: session.id,
          userId: session.userId,
          commerceOrganizationId: session.commerceOrganizationId,
        },
        env,
        app,
      })
    : null;
  if (!grant) throw new AuthConfigError("Session creation has no verified grant.");
  const store = createSessionGrantStore(
    context.context.adapter as unknown as GrantAdapter,
    env,
    app,
  );
  if (!(await store.create({ grant, sessionExpiresAt: session.expiresAt }))) {
    throw new AuthConfigError("The session grant could not be stored.");
  }
}

export function buildAuthOptions(env: AuthEnv, app: AppKind): BetterAuthOptions {
  const secureCookies = new URL(env.baseURL).protocol === "https:";
  const database = new Pool({
    connectionString: env.databaseURL,
    max: 5,
    connectionTimeoutMillis: 5_000,
    idleTimeoutMillis: 30_000,
    application_name: `teck-${app}-auth`,
  });

  const provider: GenericOAuthConfig<"keycloak"> = {
    ...keycloak({
      issuer: env.issuer,
      clientId: env.clientId,
      clientSecret: env.clientSecret,
      pkce: true,
      // The sign-in action supplies the effective scopes. Admin never adds an
      // organization hint; a chosen tenant UUID is only an operation target.
      scopes: [],
      redirectURI: `${env.baseURL}/api/auth/callback/keycloak`,
      disableProviderLogout: true,
    }),
    // Discovery must expose an issuer and JWKS before this provider registers.
    requireIdTokenVerification: true,
  };

  return {
    appName: `teck-${app}`,
    baseURL: env.baseURL,
    secret: env.secret,
    database,
    trustedOrigins: [new URL(env.baseURL).origin],
    telemetry: { enabled: false },
    emailAndPassword: { enabled: false },
    account: {
      encryptOAuthTokens: true,
      storeAccountCookie: false,
      updateAccountOnSignIn: true,
      accountLinking: { enabled: false, disableImplicitLinking: true },
    },
    session: { cookieCache: { enabled: false } },
    advanced: {
      cookiePrefix: "teck-admin",
      useSecureCookies: secureCookies,
      defaultCookieAttributes: {
        httpOnly: true,
        sameSite: "lax",
        secure: secureCookies,
        path: "/",
      },
      crossSubDomainCookies: { enabled: false },
    },
    databaseHooks: {
      account: {
        create: {
          before: (account, context) => captureAccountGrant(env, app, account, context),
        },
        update: {
          before: (account, context) => captureAccountGrant(env, app, account, context),
        },
      },
      session: {
        create: {
          after: (session, context) => storeSessionGrant({ env, app, session, context }),
        },
      },
    },
    plugins: [genericOAuth({ config: [provider] }), nextCookies()],
  };
}
