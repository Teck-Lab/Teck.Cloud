"use client";

import type { ComponentProps } from "react";
import { CheckoutControls, type CheckoutControlsProps } from "@/checkout/controls";
import type { BasketView, OfferView } from "@/commerce/contracts";
import {
  type BasketAuthority,
  BasketEditorView,
  type BasketSave,
  useBasketAuthority,
} from "@/components/basket-editor";
import { FailureNotice } from "@/components/failure-notice";

export type BasketCatalog =
  | { status: "ready"; offers: OfferView[] }
  | {
      status: "failed";
      kind: ComponentProps<typeof FailureNotice>["kind"];
      actionHref: string;
      actionLabel: string;
    };

function CheckoutPanel({
  scope,
  authority,
  submit,
  navigate,
}: {
  scope: string;
  authority: BasketAuthority;
  submit: CheckoutControlsProps["submit"];
  navigate?: (url: string) => void;
}) {
  const { basket, pending } = authority;
  const populated = basket.selection !== null;

  return (
    <section
      aria-labelledby="checkout-heading"
      className="rounded-lg border border-border bg-card p-4 shadow-sm sm:p-5"
    >
      <h2 id="checkout-heading" className="text-base font-semibold text-card-foreground">
        Checkout
      </h2>
      <p className="mt-2 text-sm text-muted-foreground">
        Checkout opens on the configured payment provider. Payment is only confirmed when the order
        later reports an active or pending state.
      </p>
      {populated ? null : (
        <p className="mt-3 text-sm text-muted-foreground">
          Choose a plan and save it before starting checkout.
        </p>
      )}
      {pending ? (
        <p aria-live="polite" className="mt-3 text-sm text-muted-foreground">
          Saving the basket before checkout.
        </p>
      ) : null}
      <fieldset className="mt-4 min-w-0 border-0 p-0" disabled={pending}>
        <legend className="sr-only">Checkout actions</legend>
        <CheckoutControls
          scope={scope}
          basket={{ revision: basket.revision, populated: populated && !pending }}
          submit={submit}
          navigate={navigate}
        />
      </fieldset>
    </section>
  );
}

/**
 * Client body of the basket page. One authority instance feeds the editor,
 * checkout eligibility and set-aside, so a saved revision is visible to every
 * consumer without a reload.
 */
export function BasketWorkspace({
  catalog,
  basket,
  scope,
  save,
  submit,
  refreshHref,
  navigate,
}: {
  catalog: BasketCatalog;
  basket: BasketView;
  scope: string;
  save: BasketSave;
  submit: CheckoutControlsProps["submit"];
  refreshHref: string;
  navigate?: (url: string) => void;
}) {
  const authority = useBasketAuthority({ basket, save, refreshHref });

  return (
    <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
      {catalog.status === "ready" ? (
        <BasketEditorView catalog={catalog.offers} authority={authority} />
      ) : (
        <FailureNotice
          kind={catalog.kind}
          actionHref={catalog.actionHref}
          actionLabel={catalog.actionLabel}
        />
      )}
      <CheckoutPanel scope={scope} authority={authority} submit={submit} navigate={navigate} />
    </div>
  );
}
