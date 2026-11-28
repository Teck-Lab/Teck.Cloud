"use client";

import { Notice } from "@teck/ui/blocks/notice";
import { Button } from "@teck/ui/components/button";

type RootErrorProps = {
  error: Error & { digest?: string };
  reset: () => void;
};

export default function RootError({ reset }: RootErrorProps) {
  return (
    <section className="mx-auto flex w-full max-w-3xl flex-col gap-4 px-6 py-12">
      <h1 className="text-2xl font-semibold tracking-tight text-foreground">
        Commerce administration
      </h1>
      <Notice tone="error">We could not load this page. Try again.</Notice>
      <div className="flex items-center gap-3">
        <Button type="button" onClick={reset}>
          Try again
        </Button>
        <a className="text-sm underline" href="/orders">
          Back to orders
        </a>
      </div>
    </section>
  );
}
