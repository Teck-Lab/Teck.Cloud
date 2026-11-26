"use client";

import { Notice } from "@teck/ui/blocks/notice";
import { Button } from "@teck/ui/components/button";
import { TextLink } from "@/components/text-link";

export default function ErrorPage({
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold tracking-tight text-foreground">
        Commerce is temporarily unavailable
      </h1>
      <p className="mt-2 text-sm text-muted-foreground">
        The console could not load this view. Your basket and checkout recovery state remain in this
        browser.
      </p>
      <div className="mt-4">
        <Notice tone="error">
          No request was retried automatically. Try again when the service is reachable.
        </Notice>
      </div>
      <div className="mt-4 flex flex-wrap items-center gap-3">
        <Button type="button" onClick={() => reset()}>
          Try again
        </Button>
        <TextLink href="/catalog">Go to catalog</TextLink>
      </div>
    </div>
  );
}
