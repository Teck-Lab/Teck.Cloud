import { afterEach, beforeEach, describe, expect, test } from "bun:test";
import { act, cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { CheckoutControls } from "./controls";
import { CHECKOUT_RECOVERY_UNCONFIRMED, CHECKOUT_SET_ASIDE_CONFIRMATION } from "./copy";
import { applyCheckoutResult, beginCheckoutIntent } from "./journal";
import {
  type CheckoutGatewayStub,
  createCheckoutGatewayStub,
  intentRow,
  ORDER_O,
  payableResult,
  problemResult,
  readIntentRow,
  readIntentRows,
  SCOPE_A,
  settleUntil,
  writeIntentRows,
} from "./testing/fixtures";
import { installMemoryIndexedDb, removeMemoryIndexedDb } from "./testing/memory-indexeddb";

beforeEach(() => {
  installMemoryIndexedDb();
});

afterEach(() => {
  cleanup();
  removeMemoryIndexedDb();
});

async function waitForReady(container: HTMLElement): Promise<void> {
  await waitFor(() => {
    expect(container.querySelector('[aria-busy="false"]')).not.toBeNull();
  });
}

function detailControls(
  order: { id: string; status: string },
  stub: CheckoutGatewayStub,
  navigations: string[],
) {
  return (
    <CheckoutControls
      navigate={(url) => {
        navigations.push(url);
      }}
      order={order}
      scope={SCOPE_A}
      submit={stub.submit}
    />
  );
}

function basketControls(
  basket: { revision: number; populated: boolean },
  stub: CheckoutGatewayStub,
  navigations: string[],
) {
  return (
    <CheckoutControls
      basket={basket}
      navigate={(url) => {
        navigations.push(url);
      }}
      scope={SCOPE_A}
      submit={stub.submit}
    />
  );
}

function nextTask(): Promise<void> {
  const { promise, resolve } = Promise.withResolvers<void>();
  setTimeout(() => {
    resolve();
  }, 0);
  return promise;
}

describe("cross-tab checkout races", () => {
  test("a committed 201 makes an older 404 stale in the other tab", async () => {
    await writeIntentRows([intentRow({ scope: SCOPE_A, basketRevision: 4, orderId: ORDER_O })]);
    const initial = await readIntentRow(SCOPE_A, 4);
    const stub = createCheckoutGatewayStub();
    const navigationsA: string[] = [];
    const navigationsB: string[] = [];
    const first = render(
      detailControls({ id: ORDER_O, status: "pendingPayment" }, stub, navigationsA),
    );
    await waitForReady(first.container);
    const second = render(
      detailControls({ id: ORDER_O, status: "pendingPayment" }, stub, navigationsB),
    );
    await waitForReady(second.container);
    const firstResume = within(first.container).getByRole("button", { name: "Resume checkout" });
    const secondResume = within(second.container).getByRole("button", { name: "Resume checkout" });
    fireEvent.click(firstResume);
    await waitFor(() => {
      expect(stub.calls).toHaveLength(1);
    });
    fireEvent.click(secondResume);
    await waitFor(() => {
      expect(stub.calls).toHaveLength(2);
    });
    expect(stub.calls[0]).toEqual(stub.calls[1]);
    if (!initial) {
      throw new Error("Expected a seeded checkout intent.");
    }
    expect(stub.calls[0]).toEqual({
      scope: SCOPE_A,
      basketRevision: 4,
      idempotencyKey: initial.idempotencyKey,
    });

    await act(async () => {
      stub.respond(1, payableResult());
    });
    await waitFor(() => {
      expect(navigationsA.length + navigationsB.length).toBe(1);
    });
    const committed = await readIntentRow(SCOPE_A, 4);
    expect(committed?.orderId).toBe(ORDER_O);
    expect(committed?.recoveryState).toBeNull();
    expect(committed?.stateFence).not.toBe(initial?.stateFence);

    await act(async () => {
      stub.respond(0, problemResult(404, "not_found"));
      await nextTask();
    });
    const after = await readIntentRow(SCOPE_A, 4);
    expect(after?.orderId).toBe(ORDER_O);
    expect(after?.recoveryState).toBeNull();
    expect(after?.stateFence).toBe(committed?.stateFence);
    expect(screen.queryByText(CHECKOUT_RECOVERY_UNCONFIRMED)).toBeNull();
    expect(screen.queryByRole("button", { name: "Set aside previous checkout" })).toBeNull();
    expect(navigationsA.length + navigationsB.length).toBe(1);
    first.unmount();
    second.unmount();

    const reloadStub = createCheckoutGatewayStub();
    const reloadNavigations: string[] = [];
    const reloaded = render(
      detailControls({ id: ORDER_O, status: "pendingPayment" }, reloadStub, reloadNavigations),
    );
    await waitForReady(reloaded.container);
    expect(screen.getByRole("button", { name: "Resume checkout" })).toBeTruthy();
  });

  test("a 404 committed first is visible until a matching 201 clears it", async () => {
    await writeIntentRows([intentRow({ scope: SCOPE_A, basketRevision: 4, orderId: ORDER_O })]);
    const initial = await readIntentRow(SCOPE_A, 4);
    const stub = createCheckoutGatewayStub();
    const navigationsA: string[] = [];
    const navigationsB: string[] = [];
    const first = render(
      detailControls({ id: ORDER_O, status: "pendingPayment" }, stub, navigationsA),
    );
    await waitForReady(first.container);
    const second = render(
      detailControls({ id: ORDER_O, status: "pendingPayment" }, stub, navigationsB),
    );
    await waitForReady(second.container);
    const firstResume = within(first.container).getByRole("button", { name: "Resume checkout" });
    const secondResume = within(second.container).getByRole("button", { name: "Resume checkout" });
    fireEvent.click(firstResume);
    await waitFor(() => {
      expect(stub.calls).toHaveLength(1);
    });
    fireEvent.click(secondResume);
    await waitFor(() => {
      expect(stub.calls).toHaveLength(2);
    });
    expect(stub.calls[0]).toEqual(stub.calls[1]);

    await act(async () => {
      stub.respond(0, problemResult(404, "not_found"));
    });
    await waitFor(() => {
      expect(screen.getAllByText(CHECKOUT_RECOVERY_UNCONFIRMED).length).toBeGreaterThan(0);
    });
    const interim = await readIntentRow(SCOPE_A, 4);
    expect(interim?.recoveryState).toBe("notFound");
    expect(interim?.orderId).toBe(ORDER_O);
    expect(interim?.stateFence).toBe(initial?.stateFence);
    expect(navigationsA.length + navigationsB.length).toBe(0);

    await act(async () => {
      stub.respond(1, payableResult());
    });
    await waitFor(() => {
      expect(navigationsA.length + navigationsB.length).toBe(1);
    });
    const final = await readIntentRow(SCOPE_A, 4);
    expect(final?.recoveryState).toBeNull();
    expect(final?.orderId).toBe(ORDER_O);
    expect(final?.stateFence).not.toBe(initial?.stateFence);
    first.unmount();
    second.unmount();

    const reloadStub = createCheckoutGatewayStub();
    const reloadNavigations: string[] = [];
    const reloaded = render(
      detailControls({ id: ORDER_O, status: "pendingPayment" }, reloadStub, reloadNavigations),
    );
    await waitForReady(reloaded.container);
    expect(screen.queryByText(CHECKOUT_RECOVERY_UNCONFIRMED)).toBeNull();
    expect(screen.getByRole("button", { name: "Resume checkout" })).toBeTruthy();
    reloaded.unmount();

    const freshStub = createCheckoutGatewayStub();
    const freshNavigations: string[] = [];
    const fresh = render(
      detailControls({ id: ORDER_O, status: "pendingPayment" }, freshStub, freshNavigations),
    );
    await waitForReady(fresh.container);
    fireEvent.click(screen.getByRole("button", { name: "Resume checkout" }));
    await waitFor(() => {
      expect(freshStub.calls).toHaveLength(1);
    });
    await act(async () => {
      freshStub.respond(0, payableResult());
    });
    await waitFor(() => {
      expect(freshNavigations).toEqual([`/orders/${ORDER_O}?checkout=unavailable`]);
    });
    expect((await readIntentRow(SCOPE_A, 4))?.recoveryState).toBeNull();

    fresh.unmount();
    const notFoundStub = createCheckoutGatewayStub();
    const notFoundNavigations: string[] = [];
    const notFoundView = render(
      detailControls({ id: ORDER_O, status: "pendingPayment" }, notFoundStub, notFoundNavigations),
    );
    await waitForReady(notFoundView.container);
    fireEvent.click(screen.getByRole("button", { name: "Resume checkout" }));
    await waitFor(() => {
      expect(notFoundStub.calls).toHaveLength(1);
    });
    await act(async () => {
      notFoundStub.respond(0, problemResult(404, "not_found"));
    });
    await screen.findByText(CHECKOUT_RECOVERY_UNCONFIRMED);
    const recovered = await readIntentRow(SCOPE_A, 4);
    expect(recovered?.orderId).toBe(ORDER_O);
    expect(recovered?.recoveryState).toBe("notFound");
    expect(screen.queryByRole("button", { name: "Set aside previous checkout" })).toBeNull();
    expect(screen.getByRole("link", { name: "Back to basket" }).getAttribute("href")).toBe(
      "/basket",
    );
  });

  test("a Resume captured before another tab's 201 refreshes instead of posting again", async () => {
    await writeIntentRows([intentRow({ scope: SCOPE_A, basketRevision: 4, orderId: ORDER_O })]);
    const initial = await readIntentRow(SCOPE_A, 4);
    const stub = createCheckoutGatewayStub();
    const navigationsA: string[] = [];
    const navigationsB: string[] = [];
    const first = render(
      detailControls({ id: ORDER_O, status: "pendingPayment" }, stub, navigationsA),
    );
    await waitForReady(first.container);
    const second = render(
      detailControls({ id: ORDER_O, status: "pendingPayment" }, stub, navigationsB),
    );
    await waitForReady(second.container);
    fireEvent.click(within(first.container).getByRole("button", { name: "Resume checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(1);
    });
    await act(async () => {
      stub.respond(0, payableResult());
    });
    await waitFor(() => {
      expect(navigationsA).toHaveLength(1);
    });
    const committed = await readIntentRow(SCOPE_A, 4);
    expect(committed?.stateFence).not.toBe(initial?.stateFence);

    const secondResume = within(second.container).getByRole("button", { name: "Resume checkout" });
    fireEvent.click(secondResume);
    expect((secondResume as HTMLButtonElement).disabled).toBe(true);
    await settleUntil(
      () =>
        !within(second.container).getByRole<HTMLButtonElement>("button", {
          name: "Resume checkout",
        }).disabled,
    );
    expect(stub.calls).toHaveLength(1);
    expect(navigationsB).toEqual([]);
    expect((await readIntentRow(SCOPE_A, 4))?.stateFence).toBe(committed?.stateFence);

    fireEvent.click(within(second.container).getByRole("button", { name: "Resume checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(2);
    });
    expect(stub.calls[1]).toEqual(stub.calls[0]);
    await act(async () => {
      stub.respond(1, payableResult());
    });
    await waitFor(() => {
      expect(navigationsB).toHaveLength(1);
    });
    first.unmount();
    second.unmount();
  });

  test("a Resume captured before another tab's 404 refreshes before replaying", async () => {
    await writeIntentRows([intentRow({ scope: SCOPE_A, basketRevision: 4, orderId: ORDER_O })]);
    const initial = await readIntentRow(SCOPE_A, 4);
    const stub = createCheckoutGatewayStub();
    const navigationsA: string[] = [];
    const navigationsB: string[] = [];
    const first = render(
      detailControls({ id: ORDER_O, status: "pendingPayment" }, stub, navigationsA),
    );
    await waitForReady(first.container);
    const second = render(
      detailControls({ id: ORDER_O, status: "pendingPayment" }, stub, navigationsB),
    );
    await waitForReady(second.container);
    fireEvent.click(within(first.container).getByRole("button", { name: "Resume checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(1);
    });
    await act(async () => {
      stub.respond(0, problemResult(404, "not_found"));
    });
    await waitFor(() => {
      expect(within(first.container).queryByText(CHECKOUT_RECOVERY_UNCONFIRMED)).not.toBeNull();
    });

    fireEvent.click(within(second.container).getByRole("button", { name: "Resume checkout" }));
    await settleUntil(
      () => within(second.container).queryByText(CHECKOUT_RECOVERY_UNCONFIRMED) !== null,
    );
    expect(stub.calls).toHaveLength(1);
    expect(navigationsB).toEqual([]);
    const recovered = await readIntentRow(SCOPE_A, 4);
    expect(recovered?.recoveryState).toBe("notFound");
    expect(recovered?.orderId).toBe(ORDER_O);
    expect(recovered?.stateFence).toBe(initial?.stateFence);

    fireEvent.click(within(second.container).getByRole("button", { name: "Resume checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(2);
    });
    expect(stub.calls[1]).toEqual(stub.calls[0]);
    await act(async () => {
      stub.respond(1, payableResult());
    });
    await waitFor(() => {
      expect(navigationsB).toHaveLength(1);
    });
    expect((await readIntentRow(SCOPE_A, 4))?.recoveryState).toBeNull();
    first.unmount();
    second.unmount();
  });

  test("two concurrent new-revision submissions reuse one committed idempotency key", async () => {
    const stub = createCheckoutGatewayStub();
    const navigationsA: string[] = [];
    const navigationsB: string[] = [];
    const first = render(basketControls({ revision: 4, populated: true }, stub, navigationsA));
    await waitForReady(first.container);
    const second = render(basketControls({ revision: 4, populated: true }, stub, navigationsB));
    await waitForReady(second.container);
    expect(screen.getAllByRole("button", { name: "Proceed to checkout" })).toHaveLength(2);
    fireEvent.click(within(first.container).getByRole("button", { name: "Proceed to checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(1);
    });
    fireEvent.click(within(second.container).getByRole("button", { name: "Proceed to checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(2);
    });
    expect(stub.calls[0]).toEqual(stub.calls[1]);
    expect(await readIntentRows()).toHaveLength(1);
    expect(await readIntentRow(SCOPE_A, 4)).not.toBeNull();
    await act(async () => {
      stub.respond(0, payableResult());
    });
    await act(async () => {
      stub.respond(1, payableResult());
      await nextTask();
    });
    await waitFor(() => {
      expect(navigationsA.length + navigationsB.length).toBe(1);
    });
    const bound = await readIntentRow(SCOPE_A, 4);
    expect(bound?.orderId).toBe(ORDER_O);
  });

  test("confirming a stale set-aside after another connection binds the intent neither parks nor submits", async () => {
    await writeIntentRows([
      intentRow({ scope: SCOPE_A, basketRevision: 4, recoveryState: "notFound" }),
    ]);
    const stub = createCheckoutGatewayStub();
    const navigations: string[] = [];
    const view = render(basketControls({ revision: 6, populated: true }, stub, navigations));
    await waitForReady(view.container);
    fireEvent.click(screen.getByRole("button", { name: "Set aside previous checkout" }));
    await screen.findByText(CHECKOUT_SET_ASIDE_CONFIRMATION);

    const begun = await beginCheckoutIntent(SCOPE_A, 4);
    if (begun.status !== "ready") {
      throw new Error("Expected a replayable checkout intent.");
    }
    expect(
      await applyCheckoutResult(
        begun.intent,
        payableResult({ checkoutUrl: "https://checkout.stripe.com/c/pay/fresh" }),
      ),
    ).toMatchObject({ kind: "bound", orderId: ORDER_O });

    const confirm = screen.getByRole<HTMLButtonElement>("button", { name: "Confirm set aside" });
    await waitFor(() => expect(confirm.disabled).toBe(false));
    fireEvent.click(confirm);
    await waitFor(() => {
      expect(Boolean(screen.queryByText(CHECKOUT_SET_ASIDE_CONFIRMATION))).toBe(false);
    });
    expect(stub.calls).toHaveLength(0);
    expect(navigations).toEqual([]);
    const row = await readIntentRow(SCOPE_A, 4);
    expect(row?.orderId).toBe(ORDER_O);
    expect(row?.recoveryState).toBeNull();
    expect(row?.stateFence).not.toBe(begun.intent.stateFence);
  });
});
