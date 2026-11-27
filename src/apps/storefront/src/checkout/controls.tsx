"use client";

import { Notice } from "@teck/ui/blocks/notice";
import { Button } from "@teck/ui/components/button";
import { useCallback, useEffect, useRef, useState } from "react";
import {
  CHECKOUT_RECOVERY_UNCONFIRMED,
  CHECKOUT_SET_ASIDE_CONFIRMATION,
  CHECKOUT_STORAGE_UNAVAILABLE,
} from "./copy";
import {
  applyCheckoutResult,
  beginCheckoutIntent,
  type CapturedCheckoutIntent,
  type CheckoutIntentRow,
  type CheckoutRetainedReason,
  captureCheckoutIntent,
  forgetCheckoutIntentsForOrder,
  listCheckoutIntents,
  parkCheckoutIntent,
  replayCheckoutIntent,
} from "./journal";
import type { CheckoutResult } from "./types";

const TEMPORARY_UNAVAILABLE = "Checkout is temporarily unavailable. Try again.";
const UNAUTHORIZED = "Sign in to continue checkout.";
const FORBIDDEN = "You don't have access to checkout.";
const DENIED = "Checkout could not be opened safely. Try again.";
const CONFLICT = "Checkout could not be completed. Review your basket and try again.";
const MALFORMED = "We couldn't confirm this checkout. Try again.";
const UNRESOLVED = "Checkout could not be started. Try again.";
const BLOCKED = "An earlier checkout needs attention before you can start another one.";
const SET_ASIDE_DONE = "Previous checkout set aside. You can start a new checkout.";

export interface CheckoutControlsProps {
  scope: string;
  basket?: { revision: number; populated: boolean };
  order?: { id: string; status: string };
  submit: (input: {
    scope: string;
    basketRevision: number;
    idempotencyKey: string;
  }) => Promise<CheckoutResult>;
  navigate?: (url: string) => void;
}

function assignLocation(url: string): void {
  if (typeof window === "undefined") {
    return;
  }
  window.location.assign(url);
}

function retainedMessage(reason: CheckoutRetainedReason): string {
  switch (reason) {
    case "denied":
      return DENIED;
    case "forbidden":
      return FORBIDDEN;
    case "unauthorized":
      return UNAUTHORIZED;
    case "conflict":
      return CONFLICT;
    case "malformed":
      return MALFORMED;
    case "unresolved":
      return UNRESOLVED;
    case "unavailable":
      return TEMPORARY_UNAVAILABLE;
  }
}

function orderHref(orderId: string, checkoutUnavailable: boolean): string {
  const base = `/orders/${encodeURIComponent(orderId)}`;
  return checkoutUnavailable ? `${base}?checkout=unavailable` : base;
}

function rowKey(row: CheckoutIntentRow): string {
  return `${row.basketRevision}:${row.stateFence}`;
}

function RecoveryNotice() {
  return (
    <Notice tone="info">
      <span>{CHECKOUT_RECOVERY_UNCONFIRMED}</span>{" "}
      <a className="underline underline-offset-2" href="/orders">
        View order history
      </a>
    </Notice>
  );
}

export function CheckoutControls({
  scope,
  basket,
  order,
  submit,
  navigate,
}: CheckoutControlsProps) {
  const [journal, setJournal] = useState<{
    scope: string;
    rows: CheckoutIntentRow[];
  } | null>(null);
  const [storageErrorScope, setStorageErrorScope] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [confirming, setConfirming] = useState<string | null>(null);
  const mounted = useRef(true);
  const scopeRef = useRef(scope);
  scopeRef.current = scope;
  const cleanupAttempted = useRef<string | null>(null);
  const refreshGeneration = useRef(0);

  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  const refresh = useCallback(async () => {
    const requestedScope = scope;
    const generation = refreshGeneration.current + 1;
    refreshGeneration.current = generation;
    try {
      const next = await listCheckoutIntents(requestedScope);
      if (
        !mounted.current ||
        scopeRef.current !== requestedScope ||
        refreshGeneration.current !== generation
      ) {
        return;
      }
      setJournal({ scope: requestedScope, rows: next });
      setStorageErrorScope(null);
    } catch {
      if (
        !mounted.current ||
        scopeRef.current !== requestedScope ||
        refreshGeneration.current !== generation
      ) {
        return;
      }
      setJournal({ scope: requestedScope, rows: [] });
      setStorageErrorScope(requestedScope);
    }
  }, [scope]);

  useEffect(() => {
    setJournal(null);
    setStorageErrorScope(null);
    setConfirming(null);
    setMessage(null);
    void refresh();
  }, [refresh]);

  const rows = journal !== null && journal.scope === scope ? journal.rows : null;
  const storageUnavailable = storageErrorScope === scope;

  const orderId = order?.id ?? null;
  const orderStatus = order?.status ?? null;

  useEffect(() => {
    if (!orderId || !orderStatus || orderStatus === "pendingPayment") {
      cleanupAttempted.current = null;
      return;
    }
    if (!rows) {
      return;
    }
    if (!rows.some((row) => row.orderId === orderId)) {
      cleanupAttempted.current = null;
      return;
    }
    const attemptKey = `${scope}\u0000${orderId}\u0000${orderStatus}`;
    if (cleanupAttempted.current === attemptKey) {
      return;
    }
    cleanupAttempted.current = attemptKey;
    void (async () => {
      try {
        await forgetCheckoutIntentsForOrder(scope, orderId);
      } catch {
        // A failed cleanup must not resurrect a resume action for a settled order.
      }
      if (mounted.current) {
        await refresh();
      }
    })();
  }, [orderId, orderStatus, rows, scope, refresh]);

  const redirect = navigate ?? assignLocation;

  const handleResult = useCallback(
    async (captured: CapturedCheckoutIntent, result: CheckoutResult) => {
      const outcome = await applyCheckoutResult(captured, result);
      if (!mounted.current || scopeRef.current !== captured.scope) {
        return;
      }
      switch (outcome.kind) {
        case "stale":
          await refresh();
          return;
        case "unavailable":
          setStorageErrorScope(captured.scope);
          setMessage(null);
          return;
        case "bound": {
          await refresh();
          if (!mounted.current || scopeRef.current !== captured.scope) {
            return;
          }
          redirect(outcome.checkoutUrl ?? orderHref(outcome.orderId, true));
          return;
        }
        case "removed": {
          await refresh();
          if (!mounted.current || scopeRef.current !== captured.scope) {
            return;
          }
          if (outcome.orderId) {
            redirect(orderHref(outcome.orderId, false));
          } else {
            setMessage(CONFLICT);
          }
          return;
        }
        case "notFound":
          await refresh();
          return;
        case "retained":
          await refresh();
          if (!mounted.current || scopeRef.current !== captured.scope) {
            return;
          }
          setMessage(retainedMessage(outcome.reason));
          return;
      }
    },
    [redirect, refresh],
  );

  const sendIntent = useCallback(
    async (captured: CapturedCheckoutIntent) => {
      const result = await submit({
        scope: captured.scope,
        basketRevision: captured.basketRevision,
        idempotencyKey: captured.idempotencyKey,
      }).catch(() => null);
      if (!mounted.current || scopeRef.current !== captured.scope) {
        return;
      }
      if (!result) {
        setMessage(TEMPORARY_UNAVAILABLE);
        await refresh();
        return;
      }
      await handleResult(captured, result);
    },
    [handleResult, refresh, submit],
  );

  const proceedCheckout = useCallback(
    async (basketRevision: number) => {
      if (!mounted.current) {
        return;
      }
      setMessage(null);
      setBusy(true);
      try {
        const begin = await beginCheckoutIntent(scope, basketRevision).catch(() => null);
        if (!begin) {
          if (mounted.current && scopeRef.current === scope) {
            setStorageErrorScope(scope);
          }
          return;
        }
        if (!mounted.current || scopeRef.current !== scope) {
          return;
        }
        if (begin.status === "blocked") {
          await refresh();
          return;
        }
        if (begin.status === "unavailable") {
          setStorageErrorScope(scope);
          return;
        }
        await sendIntent(begin.intent);
      } finally {
        if (mounted.current) {
          setBusy(false);
        }
      }
    },
    [refresh, scope, sendIntent],
  );

  const replayCheckout = useCallback(
    async (displayed: CheckoutIntentRow) => {
      if (!mounted.current || displayed.scope !== scope) {
        return;
      }
      setBusy(true);
      try {
        const outcome = await replayCheckoutIntent(displayed);
        if (!mounted.current || scopeRef.current !== scope) {
          return;
        }
        if (outcome.status === "unavailable") {
          setMessage(null);
          setStorageErrorScope(scope);
          return;
        }
        if (outcome.status === "stale") {
          await refresh();
          return;
        }
        setMessage(null);
        await sendIntent(outcome.intent);
      } finally {
        if (mounted.current) {
          setBusy(false);
        }
      }
    },
    [refresh, scope, sendIntent],
  );

  const confirmSetAside = useCallback(
    async (row: CheckoutIntentRow) => {
      if (!mounted.current) {
        return;
      }
      setMessage(null);
      setBusy(true);
      try {
        const outcome = await parkCheckoutIntent(captureCheckoutIntent(row, "replay"));
        if (!mounted.current || scopeRef.current !== scope) {
          return;
        }
        setConfirming(null);
        await refresh();
        if (mounted.current && outcome === "parked") {
          setMessage(SET_ASIDE_DONE);
        }
      } catch {
        if (!mounted.current || scopeRef.current !== scope) {
          return;
        }
        setStorageErrorScope(scope);
        setMessage(null);
      } finally {
        if (mounted.current) {
          setBusy(false);
        }
      }
    },
    [refresh, scope],
  );

  if (order) {
    if (orderStatus !== "pendingPayment") {
      return null;
    }
    if (!rows) {
      return <div aria-busy={true} />;
    }
    if (storageUnavailable) {
      return (
        <div aria-busy={false}>
          <Notice tone="error">{CHECKOUT_STORAGE_UNAVAILABLE}</Notice>
        </div>
      );
    }
    const matching = rows.filter((row) => row.orderId === orderId);
    const resumeRow = matching.length > 0 ? (matching[matching.length - 1] ?? null) : null;
    if (!resumeRow) {
      return <div aria-busy={false} />;
    }
    return (
      <div aria-busy={false} className="flex flex-col gap-3">
        {resumeRow.recoveryState === "notFound" ? <RecoveryNotice /> : null}
        <div className="flex flex-wrap items-center gap-3">
          <Button
            disabled={busy}
            onClick={() => {
              void replayCheckout(resumeRow);
            }}
            type="button"
          >
            Resume checkout
          </Button>
          {resumeRow.recoveryState === "notFound" ? (
            <a className="text-sm underline underline-offset-2" href="/basket">
              Back to basket
            </a>
          ) : null}
        </div>
        <p aria-live="polite" className="text-sm text-muted-foreground" role="status">
          {message}
        </p>
      </div>
    );
  }

  if (!basket) {
    return null;
  }

  const basketRevision = basket.revision;
  const currentRow =
    rows && basketRevision > 0
      ? (rows.find((row) => row.basketRevision === basketRevision) ?? null)
      : null;
  const blockingRow =
    rows && basketRevision > 0
      ? (rows.find(
          (row) =>
            row.orderId === null &&
            row.basketRevision !== basketRevision &&
            !(row.recoveryState === "parkedAfterNotFound" && basketRevision > row.basketRevision),
        ) ?? null)
      : null;
  const visibleRows = (rows ?? []).filter(
    (row) => row.orderId === null || row.recoveryState === "notFound",
  );
  const hasRecoveredRow = visibleRows.some((row) => row.recoveryState === "notFound");
  const ready = rows !== null;

  return (
    <div aria-busy={!ready && !storageUnavailable} className="flex flex-col gap-3">
      {storageUnavailable ? <Notice tone="error">{CHECKOUT_STORAGE_UNAVAILABLE}</Notice> : null}
      {!storageUnavailable && hasRecoveredRow ? <RecoveryNotice /> : null}
      {!storageUnavailable && basket.populated && blockingRow ? (
        <p className="text-sm text-muted-foreground">{BLOCKED}</p>
      ) : null}
      {!storageUnavailable && basket.populated && currentRow === null ? (
        <div>
          <Button
            disabled={busy || !ready || blockingRow !== null}
            onClick={() => {
              void proceedCheckout(basketRevision);
            }}
            type="button"
          >
            Proceed to checkout
          </Button>
        </div>
      ) : null}
      {visibleRows.length > 0 ? (
        <ul className="flex flex-col gap-3">
          {visibleRows.map((row) => {
            const key = rowKey(row);
            const canSetAside =
              row.orderId === null &&
              row.recoveryState === "notFound" &&
              basket.populated &&
              basketRevision > row.basketRevision;
            return (
              <li className="flex flex-col gap-2 rounded-md border border-border p-3" key={key}>
                <p className="text-sm text-muted-foreground">
                  Basket revision {row.basketRevision}
                </p>
                <div className="flex flex-wrap gap-2">
                  <Button
                    disabled={busy || storageUnavailable}
                    onClick={() => {
                      void replayCheckout(row);
                    }}
                    type="button"
                  >
                    Retry checkout
                  </Button>
                  {canSetAside && confirming !== key ? (
                    <Button
                      disabled={busy || storageUnavailable}
                      onClick={() => {
                        setMessage(null);
                        setConfirming(key);
                      }}
                      type="button"
                    >
                      Set aside previous checkout
                    </Button>
                  ) : null}
                </div>
                {canSetAside && confirming === key ? (
                  <div className="flex flex-col gap-2">
                    <p aria-live="polite" className="text-sm" role="status">
                      {CHECKOUT_SET_ASIDE_CONFIRMATION}
                    </p>
                    <div className="flex flex-wrap gap-2">
                      <Button
                        disabled={busy || storageUnavailable}
                        onClick={() => {
                          void confirmSetAside(row);
                        }}
                        type="button"
                      >
                        Confirm set aside
                      </Button>
                      <Button
                        disabled={busy}
                        onClick={() => {
                          setConfirming(null);
                        }}
                        type="button"
                      >
                        Cancel
                      </Button>
                    </div>
                  </div>
                ) : null}
              </li>
            );
          })}
        </ul>
      ) : null}
      <p aria-live="polite" className="text-sm text-muted-foreground" role="status">
        {message}
      </p>
    </div>
  );
}
