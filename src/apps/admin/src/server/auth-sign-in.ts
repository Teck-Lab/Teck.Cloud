import { assertSameOrigin, parseOrganizationAlias, safeReturnTo } from "./auth-claims";
import { BASE_AUTH_SCOPES } from "./auth-config";
import type { AppKind } from "./auth-env";

export class SignInInputError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "SignInInputError";
  }
}

export interface SignInApi {
  signInSocial(input: {
    headers: Headers;
    body: {
      provider: "keycloak";
      callbackURL: string;
      errorCallbackURL: string;
      scopes: string[];
    };
  }): Promise<{ url?: string }>;
}

/** Base scopes in both apps; Storefront adds exactly one validated organization hint. */
export function buildSignInScopes(app: AppKind, organization: unknown): string[] {
  const scopes: string[] = [...BASE_AUTH_SCOPES];
  if (app === "admin") return scopes;
  const alias = parseOrganizationAlias(organization);
  if (!alias) {
    throw new SignInInputError(
      "Enter a single organization alias using letters, digits, dots, hyphens or underscores.",
    );
  }
  scopes.push(`organization:${alias}`);
  return scopes;
}

export interface SignInRequest {
  readonly api: SignInApi;
  readonly app: AppKind;
  readonly baseURL: string;
  readonly headers: Headers;
  readonly returnTo: FormDataEntryValue | null;
  readonly organization: FormDataEntryValue | null;
}

/**
 * Starts the authorization-code flow on the server. The browser receives only
 * the provider URL; the callback sets both the PKCE state and session cookies.
 */
export async function performSignIn(request: SignInRequest): Promise<string> {
  assertSameOrigin(request.headers, request.baseURL);
  const scopes = buildSignInScopes(request.app, request.organization);
  const callbackURL = safeReturnTo(request.returnTo, request.baseURL);
  const result = await request.api.signInSocial({
    headers: request.headers,
    body: {
      provider: "keycloak",
      callbackURL,
      errorCallbackURL: "/sign-in?error=sign-in",
      scopes,
    },
  });
  if (!result.url) throw new SignInInputError("Sign-in could not be started.");
  return result.url;
}
