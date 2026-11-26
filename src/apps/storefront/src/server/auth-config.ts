import type { BetterAuthOptions } from "better-auth";
import { nextCookies } from "better-auth/next-js";
import { decryptOAuthToken } from "better-auth/oauth2";
import { type GenericOAuthConfig, genericOAuth, keycloak } from "better-auth/plugins/generic-oauth";
import { Pool } from "pg";

import { deriveSessionOrganization } from "./auth-claims";
import type { AppKind, AuthEnv } from "./auth-env";

/** Both apps request these; Storefront adds exactly one organization hint. */
export const BASE_AUTH_SCOPES = ["openid", "profile", "email"] as const;

/** Server-only, input disabled: never accepted from a browser-supplied session. */
export const STOREFRONT_SESSION_FIELDS = {
  commerceOrganizationId: { type: "string", required: false, input: false },
} as const;

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
      // The sign-in action supplies the effective scopes, including the single
      // Storefront organization:<alias> hint. Avoid duplicating them here.
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
    session: {
      cookieCache: { enabled: false },
      ...(app === "storefront" ? { additionalFields: STOREFRONT_SESSION_FIELDS } : {}),
    },
    advanced: {
      cookiePrefix: app === "storefront" ? "teck-storefront" : "teck-admin",
      useSecureCookies: secureCookies,
      defaultCookieAttributes: {
        httpOnly: true,
        sameSite: "lax",
        secure: secureCookies,
        path: "/",
      },
      crossSubDomainCookies: { enabled: false },
    },
    databaseHooks:
      app === "storefront"
        ? {
            session: {
              create: {
                before: async (session, context) => {
                  // Hooks without a request context cannot prove the callback's
                  // linked account; refuse rather than create an unscoped session.
                  if (!context) return false;
                  const accounts = await context.context.internalAdapter.findAccounts(
                    session.userId,
                  );
                  const organizationId = await deriveSessionOrganization(
                    accounts,
                    async (token) => decryptOAuthToken(token, context.context),
                    env.issuer,
                  );
                  if (!organizationId) return false;
                  return { data: { ...session, commerceOrganizationId: organizationId } };
                },
              },
            },
          }
        : undefined,
    plugins: [genericOAuth({ config: [provider] }), nextCookies()],
  };
}
