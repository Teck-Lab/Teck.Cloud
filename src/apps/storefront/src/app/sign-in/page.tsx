import { Notice } from "@teck/ui/blocks/notice";
import { Button } from "@teck/ui/components/button";
import { Field } from "@teck/ui/components/field";
import { Input } from "@teck/ui/components/input";

import { signInAction } from "./actions";

export const metadata = { title: "Sign in" };

interface SignInPageProps {
  readonly searchParams: Promise<Record<string, string | string[] | undefined>>;
}

export default async function SignInPage({ searchParams }: SignInPageProps) {
  const params = await searchParams;
  const failed = params.error !== undefined;
  const returnTo = typeof params.returnTo === "string" ? params.returnTo : "/";

  return (
    <section className="mx-auto flex w-full max-w-sm flex-col gap-6 py-10">
      <header className="flex flex-col gap-1">
        <p className="text-sm font-medium text-muted-foreground">Teck.Cloud Storefront</p>
        <h1 className="text-2xl font-semibold">Sign in</h1>
        <p className="text-sm text-muted-foreground">
          Continue with your organization identity. Only Keycloak accounts are accepted.
        </p>
      </header>

      {failed ? <Notice tone="error">Sign-in could not be completed. Try again.</Notice> : null}

      <form action={signInAction} className="flex flex-col gap-4">
        <input type="hidden" name="returnTo" value={returnTo} />
        <Field label="Organization alias" htmlFor="organization">
          <Input
            id="organization"
            name="organization"
            required
            autoComplete="organization"
            spellCheck={false}
            maxLength={128}
            pattern="[A-Za-z0-9._-]+"
            placeholder="your-organization"
          />
        </Field>
        <Button type="submit" className="w-full">
          Continue with Keycloak
        </Button>
      </form>

      <p className="text-xs text-muted-foreground">
        The organization alias selects a Keycloak scope hint. The gateway validates the actual
        organization membership in your token.
      </p>
    </section>
  );
}
