"use server";

import { isAPIError } from "better-auth/api";
import { headers } from "next/headers";
import { redirect } from "next/navigation";

import { AuthUnavailableError, getAuth } from "@/server/auth";
import { assertSameOrigin } from "@/server/auth-claims";
import { readAuthEnv } from "@/server/auth-env";
import { performSignIn } from "@/server/auth-sign-in";

const APP = "storefront" as const;

export async function signInAction(formData: FormData): Promise<never> {
  const requestHeaders = await headers();
  let target: string;
  try {
    const env = readAuthEnv(process.env, APP);
    target = await performSignIn({
      api: getAuth().api,
      app: APP,
      baseURL: env.baseURL,
      headers: requestHeaders,
      returnTo: formData.get("returnTo"),
      organization: formData.get("organization"),
    });
  } catch {
    redirect("/sign-in?error=sign-in");
  }
  redirect(target);
}

export async function signOutAction(): Promise<never> {
  const requestHeaders = await headers();
  const env = readAuthEnv(process.env, APP);
  assertSameOrigin(requestHeaders, env.baseURL);
  try {
    await getAuth().api.signOut({ headers: requestHeaders });
  } catch (error) {
    if (!isAPIError(error)) throw new AuthUnavailableError();
  }
  redirect("/sign-in");
}
