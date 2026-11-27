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
 *
 * Pagination history is keyed by the authenticated login scope only, so it
 * resets when the session changes and never reuses a checkout grant scope.
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

  let loginScope: string | null = null;
  try {
    loginScope = await appSessionScope(requestHeaders);
  } catch {
    throw new CommerceUnavailableError();
  }
  if (loginScope === null) {
    redirect(`/sign-in?returnTo=${encodeURIComponent(returnTo)}`);
  }

  return { requestHeaders, identity, paginationScope: loginScope };
}
