/**
 * Process-scoped Better Auth configuration.
 *
 * Values are read only when a server path first needs authentication. Importing
 * this module, the route or a page during `next build` must not read, validate or
 * connect to the database, issuer or gateway.
 */

export type AppKind = "storefront" | "admin";

export interface AuthEnv {
  readonly app: AppKind;
  readonly baseURL: string;
  readonly secret: string;
  readonly databaseURL: string;
  readonly issuer: string;
  readonly clientId: string;
  readonly clientSecret: string;
  readonly gatewayURL: string;
}

export class AuthConfigError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "AuthConfigError";
  }
}

type Environment = Readonly<Record<string, string | undefined>>;

const REQUIRED = [
  "BETTER_AUTH_URL",
  "BETTER_AUTH_SECRET",
  "AUTH_DATABASE_URL",
  "KEYCLOAK_ISSUER",
  "KEYCLOAK_CLIENT_ID",
  "KEYCLOAK_CLIENT_SECRET",
  "COMMERCE_GATEWAY_URL",
] as const;

function required(source: Environment, name: (typeof REQUIRED)[number]): string {
  const value = source[name]?.trim();
  if (!value) throw new AuthConfigError(`Missing required environment variable: ${name}`);
  return value;
}

function isLoopbackHostname(hostname: string): boolean {
  const host = hostname.replace(/^\[|\]$/g, "").toLowerCase();
  if (host === "localhost" || host === "::1") return true;
  return /^127\.(?:\d{1,3}\.){2}\d{1,3}$/.test(host);
}

function httpURL(name: string, raw: string): URL {
  let url: URL;
  try {
    url = new URL(raw);
  } catch {
    throw new AuthConfigError(`${name} must be an absolute URL`);
  }
  if (url.username || url.password || url.search || url.hash) {
    throw new AuthConfigError(`${name} must not contain credentials, a query or a fragment`);
  }
  const loopback = isLoopbackHostname(url.hostname);
  if (url.protocol !== "https:" && !(url.protocol === "http:" && loopback)) {
    throw new AuthConfigError(`${name} must use HTTPS except for isolated loopback HTTP`);
  }
  return url;
}

function normalizedURL(name: string, raw: string): string {
  return httpURL(name, raw).toString().replace(/\/+$/, "");
}

/**
 * Reads and validates the complete auth process configuration. Every failure
 * closes access; no value is ever echoed back or logged here.
 */
export function readAuthEnv(
  source: Environment = process.env,
  app: AppKind = "storefront",
): AuthEnv {
  const baseURL = normalizedURL("BETTER_AUTH_URL", required(source, "BETTER_AUTH_URL"));
  if (new URL(baseURL).pathname !== "/") {
    throw new AuthConfigError("BETTER_AUTH_URL must be an origin without a path");
  }
  const issuer = normalizedURL("KEYCLOAK_ISSUER", required(source, "KEYCLOAK_ISSUER"));
  const gatewayURL = normalizedURL(
    "COMMERCE_GATEWAY_URL",
    required(source, "COMMERCE_GATEWAY_URL"),
  );

  const secret = required(source, "BETTER_AUTH_SECRET");
  if (secret.trim().length < 32) {
    throw new AuthConfigError("BETTER_AUTH_SECRET must contain at least 32 characters");
  }

  const databaseURL = required(source, "AUTH_DATABASE_URL");
  let database: URL;
  try {
    database = new URL(databaseURL);
  } catch {
    throw new AuthConfigError("AUTH_DATABASE_URL must be a PostgreSQL connection URL");
  }
  if (database.protocol !== "postgres:" && database.protocol !== "postgresql:") {
    throw new AuthConfigError("AUTH_DATABASE_URL must use the postgres or postgresql scheme");
  }
  if (!database.hostname || database.pathname.length <= 1) {
    throw new AuthConfigError("AUTH_DATABASE_URL must name an auth-only PostgreSQL database");
  }

  return {
    app,
    baseURL,
    secret,
    databaseURL,
    issuer,
    clientId: required(source, "KEYCLOAK_CLIENT_ID"),
    clientSecret: required(source, "KEYCLOAK_CLIENT_SECRET"),
    gatewayURL,
  };
}
