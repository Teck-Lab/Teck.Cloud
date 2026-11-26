import { headers } from "next/headers";
import { getCommerceIdentity } from "@/server/auth";
import { readCommerceGatewayBaseUrl } from "@/server/commerce/config";
import { createCommerceGateway } from "@/server/commerce/gateway";
import { toLicenseDownload } from "@/server/commerce/license";
import {
  CommerceHttpError,
  CommerceInvalidResponseError,
  CommerceUnavailableError,
} from "@/server/commerce/problems";

export const dynamic = "force-dynamic";

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;

function plain(status: number, message: string): Response {
  return new Response(message, {
    status,
    headers: {
      "Cache-Control": "no-store",
      "Content-Type": "text/plain; charset=utf-8",
      "X-Content-Type-Options": "nosniff",
    },
  });
}

export async function GET(
  _request: Request,
  { params }: { params: Promise<{ orderId: string }> },
): Promise<Response> {
  const { orderId } = await params;
  if (!UUID_PATTERN.test(orderId)) {
    return plain(404, "License not available.");
  }

  const requestHeaders = await headers();
  let identity: { accessToken: string; scope: string } | null;
  try {
    identity = await getCommerceIdentity(requestHeaders);
  } catch {
    return plain(503, "Commerce is temporarily unavailable.");
  }
  if (identity === null) {
    return plain(401, "Sign in required.");
  }

  try {
    const gateway = createCommerceGateway({
      baseUrl: readCommerceGatewayBaseUrl(),
    });
    const order = await gateway.getOrder(identity.accessToken, orderId);
    const download = toLicenseDownload(order);
    if (download === null) {
      return plain(404, "License not available.");
    }

    return new Response(download.body, {
      status: 200,
      headers: {
        "Cache-Control": "no-store, private",
        "Content-Disposition": `attachment; filename="${download.filename}"`,
        "Content-Type": download.contentType,
        "Referrer-Policy": "no-referrer",
        "X-Content-Type-Options": "nosniff",
      },
    });
  } catch (error) {
    if (error instanceof CommerceHttpError && error.problem !== null) {
      switch (error.problem.code) {
        case "unauthenticated":
          return plain(401, "Sign in required.");
        case "forbidden":
          return plain(403, "Access denied.");
        case "not_found":
          return plain(404, "License not available.");
        case "dependency_unavailable":
          return plain(503, "Commerce is temporarily unavailable.");
        default:
          return plain(502, "License could not be loaded.");
      }
    }
    if (error instanceof CommerceInvalidResponseError || error instanceof CommerceHttpError) {
      return plain(502, "License could not be loaded.");
    }
    if (error instanceof CommerceUnavailableError) {
      return plain(503, "Commerce is temporarily unavailable.");
    }
    return plain(502, "License could not be loaded.");
  }
}
