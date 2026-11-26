"use client";

import { Notice } from "@teck/ui/blocks/notice";
import { Button } from "@teck/ui/components/button";

type OrderDetailErrorProps = {
  error: Error & { digest?: string };
  reset: () => void;
};

export default function OrderDetailError({ reset }: OrderDetailErrorProps) {
  return (
    <section className="mx-auto flex w-full max-w-5xl flex-col gap-4 px-6 py-10">
      <h1 className="text-2xl font-semibold tracking-tight text-foreground">Order</h1>
      <Notice tone="error">We could not load this order. Try again or go back to orders.</Notice>
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
