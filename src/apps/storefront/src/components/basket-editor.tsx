"use client";

import { Notice } from "@teck/ui/blocks/notice";
import { Badge } from "@teck/ui/components/badge";
import { Button } from "@teck/ui/components/button";
import { type FormEvent, useCallback, useEffect, useRef, useState } from "react";
import type {
  BasketActionResult,
  BasketSaveInput,
  BasketView,
  OfferView,
} from "@/commerce/contracts";
import { formatMoney, tierLabel } from "@/commerce/contracts";
import { TextLink } from "@/components/text-link";

export type BasketSave = (input: BasketSaveInput) => Promise<BasketActionResult>;

export type BasketFeedback = {
  tone: "info" | "error";
  message: string;
  href?: string;
  linkLabel?: string;
};

export interface BasketAuthority {
  basket: BasketView;
  pending: boolean;
  readOnly: boolean;
  feedback: BasketFeedback | null;
  saveSelection: (input: BasketSaveInput) => Promise<BasketActionResult | null>;
  clearFeedback: () => void;
}

/**
 * Single client owner of the gateway-authoritative basket. The editor,
 * checkout eligibility and set-aside all read this view. Only a successful
 * save replaces it, and a same-page server snapshot is adopted only when it is
 * newer than what this client already observed, so late renders can never
 * overwrite a confirmed revision with stale optimistic data.
 */
export function useBasketAuthority({
  basket: snapshot,
  save,
  refreshHref,
}: {
  basket: BasketView;
  save: BasketSave;
  refreshHref: string;
}): BasketAuthority {
  const [basket, setBasket] = useState<BasketView>(snapshot);
  const [pending, setPending] = useState(false);
  const [readOnly, setReadOnly] = useState(false);
  const [feedback, setFeedback] = useState<BasketFeedback | null>(null);
  const basketRef = useRef<BasketView>(snapshot);
  const pendingRef = useRef(false);
  const deferredSnapshot = useRef<BasketView | null>(null);

  const adoptSnapshot = useCallback((next: BasketView) => {
    if (next.revision <= basketRef.current.revision) {
      return;
    }
    basketRef.current = next;
    setBasket(next);
    setReadOnly(false);
    setFeedback(null);
  }, []);

  useEffect(() => {
    if (pendingRef.current) {
      deferredSnapshot.current = snapshot;
      return;
    }
    adoptSnapshot(snapshot);
  }, [snapshot, adoptSnapshot]);

  const showFailure = useCallback(
    (status: number, code?: string) => {
      if (status === 401) {
        setReadOnly(true);
        setFeedback({
          tone: "error",
          message: "Your session expired. Sign in again to change the basket.",
          href: "/sign-in",
          linkLabel: "Sign in",
        });
        return;
      }
      if (status === 403) {
        setReadOnly(true);
        setFeedback({
          tone: "error",
          message: "You do not have access to change this basket.",
        });
        return;
      }
      if (status === 409) {
        setReadOnly(true);
        setFeedback({
          tone: "error",
          message:
            code === "idempotency_conflict"
              ? "This basket request conflicts with another checkout. Refresh before changing the basket."
              : "This basket changed in another tab. Refresh to see the current selection before changing it.",
          href: refreshHref,
          linkLabel: "Refresh",
        });
        return;
      }
      if (status === 422) {
        setReadOnly(true);
        setFeedback({
          tone: "error",
          message: "One of these options is no longer offered. Refresh to see the current catalog.",
          href: refreshHref,
          linkLabel: "Refresh",
        });
        return;
      }
      if (status === 400) {
        setFeedback({
          tone: "error",
          message: "That selection was not valid. Review the plan and try again.",
        });
        return;
      }
      if (status === 503) {
        setFeedback({
          tone: "error",
          message: "The commerce service is temporarily unavailable. Try again shortly.",
        });
        return;
      }
      setFeedback({
        tone: "error",
        message: "The basket could not be saved. Refresh and try again.",
        href: refreshHref,
        linkLabel: "Refresh",
      });
    },
    [refreshHref],
  );

  const clearFeedback = useCallback(() => {
    setFeedback(null);
  }, []);

  const saveSelection = useCallback(
    async (input: BasketSaveInput): Promise<BasketActionResult | null> => {
      if (pendingRef.current) {
        return null;
      }
      pendingRef.current = true;
      setPending(true);
      setFeedback(null);
      try {
        const result = await save(input);
        if ("basket" in result) {
          const saved = result.basket;
          basketRef.current = saved;
          setBasket(saved);
          setFeedback({
            tone: "info",
            message: `Basket saved. Current total ${formatMoney(saved.total)}.`,
          });
          return result;
        }
        showFailure(result.status, result.code);
        return result;
      } catch {
        setFeedback({
          tone: "error",
          message: "The basket could not be saved. Try again.",
        });
        return null;
      } finally {
        pendingRef.current = false;
        setPending(false);
        const deferred = deferredSnapshot.current;
        deferredSnapshot.current = null;
        if (deferred !== null) {
          adoptSnapshot(deferred);
        }
      }
    },
    [adoptSnapshot, save, showFailure],
  );

  return { basket, pending, readOnly, feedback, saveSelection, clearFeedback };
}

export function BasketEditorView({
  catalog,
  authority,
}: {
  catalog: OfferView[];
  authority: BasketAuthority;
}) {
  const { basket, pending, readOnly, feedback } = authority;
  const [offerId, setOfferId] = useState<string | null>(
    basket.selection?.offerId ?? catalog[0]?.offerId ?? null,
  );
  const [addOnCodes, setAddOnCodes] = useState<string[]>(basket.selection?.addOnCodes ?? []);
  const appliedRevision = useRef(basket.revision);

  useEffect(() => {
    if (appliedRevision.current === basket.revision) {
      return;
    }
    appliedRevision.current = basket.revision;
    if (basket.selection !== null) {
      setOfferId(basket.selection.offerId);
      setAddOnCodes([...basket.selection.addOnCodes]);
      return;
    }
    setAddOnCodes([]);
    setOfferId((current) =>
      current !== null && catalog.some((offer) => offer.offerId === current)
        ? current
        : (catalog[0]?.offerId ?? null),
    );
  }, [basket, catalog]);

  const selectedOffer = catalog.find((offer) => offer.offerId === offerId) ?? null;
  const eligibleAddOns = selectedOffer?.addOns ?? [];
  const eligibleCodes = new Set(eligibleAddOns.map((addOn) => addOn.code));
  const visibleAddOnCodes = addOnCodes.filter((code) => eligibleCodes.has(code));
  const hasUnavailableAddOns = addOnCodes.length !== visibleAddOnCodes.length;
  const disabled = pending || readOnly;

  function chooseOffer(nextOfferId: string) {
    const offer = catalog.find((candidate) => candidate.offerId === nextOfferId);
    const allowed = new Set(offer?.addOns.map((addOn) => addOn.code) ?? []);
    setOfferId(nextOfferId);
    setAddOnCodes((current) => current.filter((code) => allowed.has(code)));
    authority.clearFeedback();
  }

  function toggleAddOn(code: string, checked: boolean) {
    setAddOnCodes((current) => {
      if (checked) {
        return current.includes(code) ? current : [...current, code];
      }
      return current.filter((candidate) => candidate !== code);
    });
    authority.clearFeedback();
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (disabled || offerId === null) {
      return;
    }
    void authority.saveSelection({
      selection: { offerId, addOnCodes: visibleAddOnCodes },
      expectedRevision: basket.revision,
    });
  }

  function handleClear() {
    if (disabled || basket.selection === null) {
      return;
    }
    void authority.saveSelection({ selection: null, expectedRevision: basket.revision });
  }

  return (
    <section aria-labelledby="basket-editor-heading" className="space-y-4">
      <div className="rounded-lg border border-border bg-card p-4 shadow-sm sm:p-5">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <h2 id="basket-editor-heading" className="text-base font-semibold text-card-foreground">
            Plan selection
          </h2>
          <p className="text-sm text-muted-foreground">
            Saved total{" "}
            <span className="font-medium text-foreground">{formatMoney(basket.total)}</span> ·
            revision {basket.revision}
          </p>
        </div>

        <form className="mt-4 space-y-5" onSubmit={handleSubmit} aria-busy={pending}>
          <fieldset disabled={disabled}>
            <legend className="text-sm font-medium text-foreground">Database tier</legend>
            {catalog.length === 0 ? (
              <p className="mt-2 text-sm text-muted-foreground">
                No plans are currently available for your organization.
              </p>
            ) : (
              <div className="mt-2 grid gap-2 sm:grid-cols-2">
                {catalog.map((offer) => {
                  const checked = offer.offerId === offerId;
                  return (
                    <label
                      key={offer.offerId}
                      className={`flex cursor-pointer items-start gap-3 rounded-md border p-3 transition-colors focus-within:ring-2 focus-within:ring-ring focus-within:ring-offset-2 ${
                        checked
                          ? "border-primary bg-accent text-accent-foreground"
                          : "border-border bg-background hover:bg-accent"
                      }`}
                    >
                      <input
                        type="radio"
                        name="offer"
                        value={offer.offerId}
                        checked={checked}
                        onChange={() => chooseOffer(offer.offerId)}
                        className="mt-1 h-4 w-4 shrink-0 border-input text-primary focus-visible:outline-none"
                      />
                      <span className="min-w-0 flex-1">
                        <span className="flex flex-wrap items-center gap-2">
                          <span className="text-sm font-medium">{offer.displayName}</span>
                          <Badge>{tierLabel(offer.tier)}</Badge>
                        </span>
                        <span className="mt-1 block text-sm text-muted-foreground">
                          {formatMoney(offer.basePrice)} per month
                        </span>
                      </span>
                    </label>
                  );
                })}
              </div>
            )}
          </fieldset>

          {selectedOffer !== null && eligibleAddOns.length > 0 ? (
            <fieldset disabled={disabled}>
              <legend className="text-sm font-medium text-foreground">
                Add-ons for {selectedOffer.displayName}
              </legend>
              <div className="mt-2 grid gap-2 sm:grid-cols-2">
                {eligibleAddOns.map((addOn) => {
                  const checked = visibleAddOnCodes.includes(addOn.code);
                  return (
                    <label
                      key={addOn.code}
                      className="flex cursor-pointer items-start gap-3 rounded-md border border-border bg-background p-3 hover:bg-accent focus-within:ring-2 focus-within:ring-ring focus-within:ring-offset-2"
                    >
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={(event) => toggleAddOn(addOn.code, event.target.checked)}
                        className="mt-0.5 h-4 w-4 shrink-0 rounded border-input text-primary focus-visible:outline-none"
                      />
                      <span className="min-w-0 flex-1">
                        <span className="block text-sm font-medium">{addOn.displayName}</span>
                        <span className="mt-1 block text-sm text-muted-foreground">
                          {formatMoney(addOn.price)} per month
                        </span>
                      </span>
                    </label>
                  );
                })}
              </div>
            </fieldset>
          ) : null}

          {hasUnavailableAddOns ? (
            <p className="text-sm text-muted-foreground">
              Add-ons that are no longer offered will be removed when you save.
            </p>
          ) : null}

          <div className="flex flex-wrap items-center gap-2">
            <Button type="submit" disabled={disabled || offerId === null}>
              Save selection
            </Button>
            <Button
              type="button"
              onClick={handleClear}
              disabled={disabled || basket.selection === null}
            >
              Clear basket
            </Button>
          </div>
        </form>

        <div aria-live="polite" className="mt-4">
          {feedback === null ? null : (
            <Notice tone={feedback.tone}>
              <span>{feedback.message}</span>
              {feedback.href === undefined ? null : (
                <>
                  {" "}
                  <TextLink href={feedback.href}>{feedback.linkLabel ?? "Continue"}</TextLink>
                </>
              )}
            </Notice>
          )}
        </div>
      </div>
    </section>
  );
}

export function BasketEditor({
  catalog,
  basket,
  save,
  refreshHref,
}: {
  catalog: OfferView[];
  basket: BasketView;
  save: BasketSave;
  refreshHref: string;
}) {
  const authority = useBasketAuthority({ basket, save, refreshHref });
  return <BasketEditorView catalog={catalog} authority={authority} />;
}
