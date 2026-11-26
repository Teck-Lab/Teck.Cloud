import "server-only";

import { headers } from "next/headers";
import { redirect } from "next/navigation";
import { appSessionScope, getCommerceIdentity } from "@/server/auth";
import { CommerceUnavailableError } from "./problems";

export type StorefrontCommerceSession = {
  requestHeaders: Headers;
  identity: { accessToken: string; scope: string };
  paginationScope: string;
};

/**
 * Resolves the current Better Auth session into the opaque commerce identity.
 * Missing or ambiguous sessions redirect to the same-app sign-in; auth-store
 * failures fail closed before any gateway request.
 */
export async function requireCommerceSession(
  returnTo = "/catalog",
): Promise<StorefrontCommerceSession> {
  const requestHeaders = await headers();

  let identity: { accessToken: string; scope: string } | null;
  try {
    identity = await getCommerceIdentity(requestHeaders);
  } catch {
    throw new CommerceUnavailableError();
  }

  if (identity === null) {
    redirect(`/sign-in?returnTo=${encodeURIComponent(returnTo)}`);
  }

  let paginationScope = identity.scope;
  try {
    paginationScope = (await appSessionScope(requestHeaders)) ?? identity.scope;
  } catch {
    // The checkout scope is still session-derived; pagination history can use it.
  }

  return { requestHeaders, identity, paginationScope };
}
