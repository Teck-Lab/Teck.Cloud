import type { BetterAuthOptions } from "better-auth";
import { nextCookies } from "better-auth/next-js";
import { type GenericOAuthConfig, genericOAuth, keycloak } from "better-auth/plugins/generic-oauth";
import { Pool } from "pg";

import type { AppKind, AuthEnv } from "./auth-env";

/** Both apps request these; Storefront adds exactly one organization hint. */
export const BASE_AUTH_SCOPES = ["openid", "profile", "email"] as const;

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
    plugins: [genericOAuth({ config: [provider] }), nextCookies()],
  };
}
