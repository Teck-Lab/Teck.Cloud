import { afterEach, beforeEach, describe, expect, test } from "bun:test";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { CheckoutControls } from "./controls";
import { CHECKOUT_RECOVERY_UNCONFIRMED, CHECKOUT_STORAGE_UNAVAILABLE } from "./copy";
import {
  CHECKOUT_URL,
  type CheckoutGatewayStub,
  createCheckoutGatewayStub,
  intentRow,
  KEY_B,
  ORDER_O,
  payableResult,
  problemResult,
  readIntentRow,
  readIntentRows,
  SCOPE_A,
  SCOPE_B,
  writeIntentRows,
} from "./testing/fixtures";
import { installMemoryIndexedDb, removeMemoryIndexedDb } from "./testing/memory-indexeddb";

const TEMPORARY = "Checkout is temporarily unavailable. Try again.";
const DENIED = "Checkout could not be opened safely. Try again.";
const UNRESOLVED = "Checkout could not be started. Try again.";

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

function basketControls(
  basket: { revision: number; populated: boolean },
  stub: CheckoutGatewayStub,
  navigations: string[],
  scope = SCOPE_A,
) {
  return (
    <CheckoutControls
      basket={basket}
      navigate={(url) => {
        navigations.push(url);
      }}
      scope={scope}
      submit={stub.submit}
    />
  );
}

function orderControls(
  order: { id: string; status: string },
  stub: CheckoutGatewayStub,
  navigations: string[],
  scope = SCOPE_A,
) {
  return (
    <CheckoutControls
      navigate={(url) => {
        navigations.push(url);
      }}
      order={order}
      scope={scope}
      submit={stub.submit}
    />
  );
}

describe("checkout controls recovery", () => {
  test("recovers a lost response with the original key after reloading an empty newer basket", async () => {
    const stub = createCheckoutGatewayStub();
    const navigations: string[] = [];
    const first = render(basketControls({ revision: 4, populated: true }, stub, navigations));
    await waitForReady(first.container);
    fireEvent.click(screen.getByRole("button", { name: "Proceed to checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(1);
    });
    await act(async () => {
      stub.fail(0);
    });
    await screen.findByText(TEMPORARY);
    expect(navigations).toEqual([]);
    const lost = await readIntentRow(SCOPE_A, 4);
    if (!lost) {
      throw new Error("Expected the unbound checkout intent to survive a lost response.");
    }
    expect(lost.orderId).toBeNull();
    expect(lost.recoveryState).toBeNull();
    const firstCall = stub.calls[0];
    if (!firstCall) {
      throw new Error("Expected one checkout request.");
    }
    expect(lost.idempotencyKey).toBe(firstCall.idempotencyKey);
    first.unmount();

    const retryStub = createCheckoutGatewayStub();
    const retryNavigations: string[] = [];
    const reloaded = render(
      basketControls({ revision: 5, populated: false }, retryStub, retryNavigations),
    );
    await waitForReady(reloaded.container);
    fireEvent.click(screen.getByRole("button", { name: "Retry checkout" }));
    await waitFor(() => {
      expect(retryStub.calls).toHaveLength(1);
    });
    expect(retryStub.calls[0]).toEqual({
      scope: SCOPE_A,
      basketRevision: 4,
      idempotencyKey: lost.idempotencyKey,
    });
    await act(async () => {
      retryStub.respond(0, payableResult());
    });
    await waitFor(() => {
      expect(retryNavigations).toEqual([`/orders/${ORDER_O}?checkout=unavailable`]);
    });
    const bound = await readIntentRow(SCOPE_A, 4);
    expect(bound?.orderId).toBe(ORDER_O);
    expect(bound?.recoveryState).toBeNull();
    expect(bound?.stateFence).not.toBe(lost.stateFence);
  });

  test("resumes only its exact persisted intent, redirects after commit, and cleans up a paid order", async () => {
    await writeIntentRows([intentRow({ scope: SCOPE_A, basketRevision: 4, orderId: ORDER_O })]);
    const stub = createCheckoutGatewayStub();
    const navigations: string[] = [];
    const detail = render(
      orderControls({ id: ORDER_O, status: "pendingPayment" }, stub, navigations),
    );
    await waitForReady(detail.container);
    fireEvent.click(screen.getByRole("button", { name: "Resume checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(1);
    });
    const resumeCall = stub.calls[0];
    if (!resumeCall) {
      throw new Error("Expected a resume checkout request.");
    }
    expect(resumeCall.scope).toBe(SCOPE_A);
    expect(resumeCall.basketRevision).toBe(4);
    expect(resumeCall.idempotencyKey).toHaveLength(36);
    const beforeRedirect = await readIntentRow(SCOPE_A, 4);
    await act(async () => {
      stub.respond(0, payableResult({ checkoutUrl: CHECKOUT_URL }));
    });
    await waitFor(() => {
      expect(navigations).toEqual([CHECKOUT_URL]);
    });
    const bound = await readIntentRow(SCOPE_A, 4);
    expect(bound?.orderId).toBe(ORDER_O);
    expect(bound?.stateFence).not.toBe(beforeRedirect?.stateFence);
    detail.unmount();

    const paidStub = createCheckoutGatewayStub();
    const paidNavigations: string[] = [];
    render(orderControls({ id: ORDER_O, status: "active" }, paidStub, paidNavigations));
    await waitFor(async () => {
      expect(await readIntentRow(SCOPE_A, 4)).toBeNull();
    });
    expect(screen.queryByRole("button", { name: "Resume checkout" })).toBeNull();
    expect(paidNavigations).toEqual([]);
    expect(paidStub.calls).toHaveLength(0);
  });

  test("a denied off-origin checkout URL never redirects and is never written to disk", async () => {
    const stub = createCheckoutGatewayStub();
    const navigations: string[] = [];
    const view = render(basketControls({ revision: 4, populated: true }, stub, navigations));
    await waitForReady(view.container);
    fireEvent.click(screen.getByRole("button", { name: "Proceed to checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(1);
    });
    await act(async () => {
      stub.respond(
        0,
        payableResult({ checkoutUrl: "https://provider.example/pay", checkoutDenied: true }),
      );
    });
    await screen.findByText(DENIED);
    expect(navigations).toEqual([]);
    const row = await readIntentRow(SCOPE_A, 4);
    expect(row?.orderId).toBeNull();
    expect(row?.recoveryState).toBeNull();
    expect(JSON.stringify(await readIntentRows())).not.toContain("provider.example");
  });

  test("redirects to the explicit isolated simulator checkout origin after commit", async () => {
    const stub = createCheckoutGatewayStub();
    const navigations: string[] = [];
    const view = render(basketControls({ revision: 4, populated: true }, stub, navigations));
    await waitForReady(view.container);
    fireEvent.click(screen.getByRole("button", { name: "Proceed to checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(1);
    });
    const simulatorUrl = "http://127.0.0.1:12111/c/pay/sim_1";
    await act(async () => {
      stub.respond(0, payableResult({ checkoutUrl: simulatorUrl }));
    });
    await waitFor(() => {
      expect(navigations).toEqual([simulatorUrl]);
    });
    expect((await readIntentRow(SCOPE_A, 4))?.orderId).toBe(ORDER_O);
  });

  test("a 503 preserves the tuple and allows an explicit retry", async () => {
    const stub = createCheckoutGatewayStub();
    const navigations: string[] = [];
    const view = render(basketControls({ revision: 4, populated: true }, stub, navigations));
    await waitForReady(view.container);
    fireEvent.click(screen.getByRole("button", { name: "Proceed to checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(1);
    });
    await act(async () => {
      stub.respond(0, problemResult(503, "dependency_unavailable"));
    });
    await screen.findByText(TEMPORARY);
    const retained = await readIntentRow(SCOPE_A, 4);
    expect(retained?.orderId).toBeNull();
    expect(navigations).toEqual([]);

    fireEvent.click(screen.getByRole("button", { name: "Retry checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(2);
    });
    expect(stub.calls[1]?.idempotencyKey).toBe(retained?.idempotencyKey);
    await act(async () => {
      stub.respond(1, payableResult());
    });
    await waitFor(() => {
      expect(navigations).toEqual([`/orders/${ORDER_O}?checkout=unavailable`]);
    });
  });

  test("storage that is unavailable or holds an invalid row fails closed without a POST", async () => {
    removeMemoryIndexedDb();
    const stub = createCheckoutGatewayStub();
    const navigations: string[] = [];
    const missing = render(basketControls({ revision: 4, populated: true }, stub, navigations));
    await screen.findByText(CHECKOUT_STORAGE_UNAVAILABLE);
    expect(screen.queryByRole("button", { name: "Proceed to checkout" })).toBeNull();
    expect(stub.calls).toHaveLength(0);
    missing.unmount();

    installMemoryIndexedDb();
    await writeIntentRows([
      {
        scope: SCOPE_A,
        basketRevision: 4,
        idempotencyKey: KEY_B,
        orderId: null,
        recoveryState: null,
      },
    ]);
    const invalidStub = createCheckoutGatewayStub();
    const invalidNavigations: string[] = [];
    render(basketControls({ revision: 4, populated: true }, invalidStub, invalidNavigations));
    await screen.findByText(CHECKOUT_STORAGE_UNAVAILABLE);
    expect(screen.queryByRole("button", { name: "Proceed to checkout" })).toBeNull();
    expect(invalidStub.calls).toHaveLength(0);
  });

  test("another scope never sees or reuses a resumable record", async () => {
    await writeIntentRows([
      intentRow({
        scope: SCOPE_B,
        basketRevision: 4,
        idempotencyKey: KEY_B,
        orderId: ORDER_O,
      }),
    ]);
    const detailStub = createCheckoutGatewayStub();
    const detailNavigations: string[] = [];
    const detail = render(
      orderControls({ id: ORDER_O, status: "pendingPayment" }, detailStub, detailNavigations),
    );
    await waitForReady(detail.container);
    expect(screen.queryByRole("button", { name: "Resume checkout" })).toBeNull();
    expect(detailStub.calls).toHaveLength(0);
    detail.unmount();

    const basketStub = createCheckoutGatewayStub();
    const basketNavigations: string[] = [];
    const basket = render(
      basketControls({ revision: 5, populated: true }, basketStub, basketNavigations),
    );
    await waitForReady(basket.container);
    await waitFor(() => {
      const button = screen.getByRole("button", {
        name: "Proceed to checkout",
      }) as HTMLButtonElement;
      expect(button.disabled).toBe(false);
    });
    fireEvent.click(screen.getByRole("button", { name: "Proceed to checkout" }));
    await waitFor(() => {
      expect(basketStub.calls).toHaveLength(1);
    });
    expect(basketStub.calls[0]?.idempotencyKey).not.toBe(KEY_B);
    await act(async () => {
      basketStub.respond(0, payableResult());
    });
    const otherScope = await readIntentRow(SCOPE_B, 4);
    expect(otherScope?.orderId).toBe(ORDER_O);
    expect(otherScope?.idempotencyKey).toBe(KEY_B);
  });

  test("an initial 404 leaves an ordinary retry and never announces scoped recovery", async () => {
    const stub = createCheckoutGatewayStub();
    const navigations: string[] = [];
    const view = render(basketControls({ revision: 4, populated: true }, stub, navigations));
    await waitForReady(view.container);
    fireEvent.click(screen.getByRole("button", { name: "Proceed to checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(1);
    });
    await act(async () => {
      stub.respond(0, problemResult(404, "not_found"));
    });
    await screen.findByText(UNRESOLVED);
    expect(screen.queryByText(CHECKOUT_RECOVERY_UNCONFIRMED)).toBeNull();
    const row = await readIntentRow(SCOPE_A, 4);
    expect(row?.recoveryState).toBeNull();
    expect(row?.orderId).toBeNull();
    expect(navigations).toEqual([]);
    view.unmount();

    const newer = render(basketControls({ revision: 6, populated: true }, stub, navigations));
    await waitForReady(newer.container);
    const proceed = screen.getByRole("button", {
      name: "Proceed to checkout",
    }) as HTMLButtonElement;
    expect(proceed.disabled).toBe(true);
    fireEvent.click(proceed);
    expect(stub.calls).toHaveLength(1);
    expect(screen.queryByRole("button", { name: "Set aside previous checkout" })).toBeNull();
    expect(screen.getByRole("button", { name: "Retry checkout" })).toBeTruthy();
    expect(await readIntentRow(SCOPE_A, 6)).toBeNull();
  });
});
