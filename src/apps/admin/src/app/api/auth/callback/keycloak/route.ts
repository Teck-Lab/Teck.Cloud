import { getAuth } from "@/server/auth";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const CALLBACK_PATH = "/api/auth/callback/keycloak";

/**
 * The only Better Auth HTTP endpoint this app exposes. Generic Better Auth
 * endpoints (including token and refresh endpoints) have no route and 404.
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
    return await getAuth().handler(request);
  } catch {
    return new Response("Authentication is temporarily unavailable.", {
      status: 503,
      headers: { "cache-control": "no-store" },
    });
  }
}
