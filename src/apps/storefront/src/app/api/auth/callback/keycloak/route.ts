import { getAuth } from "@/server/auth";
import { assertVerifiedKeycloakProvider } from "@/server/auth-config";
import { readAuthEnv } from "@/server/auth-env";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const CALLBACK_PATH = "/api/auth/callback/keycloak";

/**
 * The only Better Auth HTTP endpoint this app exposes. Generic Better Auth
 * endpoints (including token and refresh endpoints) have no route and 404.
 *
 * Before dispatch, the registered provider must prove the configured issuer,
 * a discovery JWKS and request-bound ID-token nonce. A discovery document that
 * reports another issuer therefore fails closed instead of becoming trusted.
 */
export async function GET(request: Request): Promise<Response> {
  const url = new URL(request.url);
  if (url.pathname !== CALLBACK_PATH) {
    return new Response("Not found", {
      status: 404,
      headers: { "cache-control": "no-store" },
    });
  }
  try {
    const auth = getAuth();
    const env = readAuthEnv(process.env, "storefront");
    await assertVerifiedKeycloakProvider(auth, env);
    return await auth.handler(request);
  } catch {
    return new Response("Authentication is temporarily unavailable.", {
      status: 503,
      headers: { "cache-control": "no-store" },
    });
  }
}
