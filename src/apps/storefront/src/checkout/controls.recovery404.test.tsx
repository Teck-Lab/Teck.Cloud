import { afterEach, beforeEach, describe, expect, test } from "bun:test";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { CheckoutControls } from "./controls";
import {
  CHECKOUT_RECOVERY_UNCONFIRMED,
  CHECKOUT_SET_ASIDE_CONFIRMATION,
  CHECKOUT_STORAGE_UNAVAILABLE,
} from "./copy";
import {
  type CheckoutGatewayStub,
  createCheckoutGatewayStub,
  FENCE_B,
  intentRow,
  KEY_B,
  ORDER_O,
  payableResult,
  problemResult,
  readIntentRow,
  readIntentRows,
  SCOPE_A,
  writeIntentRows,
} from "./testing/fixtures";
import { installMemoryIndexedDb, removeMemoryIndexedDb } from "./testing/memory-indexeddb";

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

function controls(
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

async function startAndRecoverLostResponse(stub: CheckoutGatewayStub, navigations: string[]) {
  const view = render(controls({ revision: 4, populated: true }, stub, navigations));
  await waitForReady(view.container);
  fireEvent.click(screen.getByRole("button", { name: "Proceed to checkout" }));
  await waitFor(() => {
    expect(stub.calls).toHaveLength(1);
  });
  await act(async () => {
    stub.fail(0);
  });
  view.unmount();
  return stub.calls[0];
}

describe("scoped replay 404 recovery", () => {
  test("retry, set-aside confirmation, a fresh newer checkout and a still-parked original intent", async () => {
    const stub = createCheckoutGatewayStub();
    const navigations: string[] = [];
    const initialCall = await startAndRecoverLostResponse(stub, navigations);
    if (!initialCall) {
      throw new Error("Expected an initial checkout request.");
    }
    const initialRow = await readIntentRow(SCOPE_A, 4);
    if (!initialRow) {
      throw new Error("Expected an unbound checkout intent after a lost response.");
    }

    const retryView = render(controls({ revision: 5, populated: false }, stub, navigations));
    await waitForReady(retryView.container);
    fireEvent.click(screen.getByRole("button", { name: "Retry checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(2);
    });
    expect(stub.calls[1]).toEqual(initialCall);
    await act(async () => {
      stub.respond(1, problemResult(404, "not_found"));
    });
    await screen.findByText(CHECKOUT_RECOVERY_UNCONFIRMED);
    expect(screen.getByRole("link", { name: "View order history" }).getAttribute("href")).toBe(
      "/orders",
    );
    expect(screen.queryByRole("button", { name: "Set aside previous checkout" })).toBeNull();
    const recovered = await readIntentRow(SCOPE_A, 4);
    expect(recovered?.recoveryState).toBe("notFound");
    expect(recovered?.stateFence).toBe(initialRow.stateFence);
    expect(recovered?.idempotencyKey).toBe(initialRow.idempotencyKey);
    expect(stub.calls).toHaveLength(2);
    retryView.unmount();

    const reloaded = render(controls({ revision: 5, populated: false }, stub, navigations));
    await waitForReady(reloaded.container);
    await screen.findByText(CHECKOUT_RECOVERY_UNCONFIRMED);
    expect((await readIntentRow(SCOPE_A, 4))?.recoveryState).toBe("notFound");
    expect(screen.queryByRole("button", { name: "Set aside previous checkout" })).toBeNull();
    reloaded.unmount();

    const sameRevision = render(controls({ revision: 4, populated: true }, stub, navigations));
    await waitForReady(sameRevision.container);
    await screen.findByText(CHECKOUT_RECOVERY_UNCONFIRMED);
    expect(screen.queryByRole("button", { name: "Proceed to checkout" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Set aside previous checkout" })).toBeNull();
    expect(screen.getByRole("button", { name: "Retry checkout" })).toBeTruthy();
    sameRevision.unmount();

    const newer = render(controls({ revision: 6, populated: true }, stub, navigations));
    await waitForReady(newer.container);
    await screen.findByText(CHECKOUT_RECOVERY_UNCONFIRMED);
    const blockedProceed = screen.getByRole("button", {
      name: "Proceed to checkout",
    }) as HTMLButtonElement;
    expect(blockedProceed.disabled).toBe(true);
    fireEvent.click(blockedProceed);
    expect(stub.calls).toHaveLength(2);
    expect(await readIntentRows()).toHaveLength(1);

    fireEvent.click(screen.getByRole("button", { name: "Set aside previous checkout" }));
    await screen.findByText(CHECKOUT_SET_ASIDE_CONFIRMATION);
    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
    expect(screen.queryByText(CHECKOUT_SET_ASIDE_CONFIRMATION)).toBeNull();
    expect(
      (
        screen.getByRole("button", {
          name: "Proceed to checkout",
        }) as HTMLButtonElement
      ).disabled,
    ).toBe(true);
    expect(stub.calls).toHaveLength(2);

    fireEvent.click(screen.getByRole("button", { name: "Set aside previous checkout" }));
    await screen.findByText(CHECKOUT_SET_ASIDE_CONFIRMATION);
    fireEvent.click(screen.getByRole("button", { name: "Confirm set aside" }));
    await screen.findByText("Previous checkout set aside. You can start a new checkout.");
    expect(stub.calls).toHaveLength(2);
    const parked = await readIntentRow(SCOPE_A, 4);
    expect(parked?.recoveryState).toBe("parkedAfterNotFound");
    expect(parked?.idempotencyKey).toBe(initialRow.idempotencyKey);
    expect(parked?.stateFence).not.toBe(initialRow.stateFence);
    const enabledProceed = screen.getByRole("button", {
      name: "Proceed to checkout",
    }) as HTMLButtonElement;
    expect(enabledProceed.disabled).toBe(false);

    fireEvent.click(enabledProceed);
    await waitFor(() => {
      expect(stub.calls).toHaveLength(3);
    });
    const freshCall = stub.calls[2];
    if (!freshCall) {
      throw new Error("Expected a fresh checkout request.");
    }
    expect(freshCall.basketRevision).toBe(6);
    expect(freshCall.idempotencyKey).not.toBe(initialRow.idempotencyKey);
    expect(freshCall.idempotencyKey).not.toBe(initialCall.idempotencyKey);
    await act(async () => {
      stub.respond(2, payableResult());
    });
    await waitFor(() => {
      expect(navigations).toEqual([`/orders/${ORDER_O}?checkout=unavailable`]);
    });
    newer.unmount();

    const parkedRetry = render(controls({ revision: 7, populated: true }, stub, navigations));
    await waitForReady(parkedRetry.container);
    fireEvent.click(screen.getByRole("button", { name: "Retry checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(4);
    });
    expect(stub.calls[3]).toEqual({
      scope: SCOPE_A,
      basketRevision: 4,
      idempotencyKey: initialRow.idempotencyKey,
    });
    await act(async () => {
      stub.respond(3, problemResult(404, "not_found"));
    });
    await waitFor(async () => {
      expect((await readIntentRow(SCOPE_A, 4))?.recoveryState).toBe("parkedAfterNotFound");
    });
    expect(screen.queryByText(CHECKOUT_RECOVERY_UNCONFIRMED)).toBeNull();
    expect(screen.queryByRole("button", { name: "Set aside previous checkout" })).toBeNull();
    await waitFor(() => {
      const proceed = screen.getByRole<HTMLButtonElement>("button", {
        name: "Proceed to checkout",
      });
      expect(proceed.disabled).toBe(false);
    });

    fireEvent.click(screen.getByRole("button", { name: "Retry checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(5);
    });
    await act(async () => {
      stub.respond(4, payableResult());
    });
    await waitFor(async () => {
      const row = await readIntentRow(SCOPE_A, 4);
      expect(row?.orderId).toBe(ORDER_O);
      expect(row?.recoveryState).toBeNull();
    });
  });

  test("a failed set-aside commit shows the storage error and never submits", async () => {
    await writeIntentRows([
      intentRow({ scope: SCOPE_A, basketRevision: 4, recoveryState: "notFound" }),
    ]);
    const stub = createCheckoutGatewayStub();
    const navigations: string[] = [];
    const view = render(controls({ revision: 6, populated: true }, stub, navigations));
    await waitForReady(view.container);
    fireEvent.click(screen.getByRole("button", { name: "Set aside previous checkout" }));
    await screen.findByText(CHECKOUT_SET_ASIDE_CONFIRMATION);
    removeMemoryIndexedDb();
    fireEvent.click(screen.getByRole("button", { name: "Confirm set aside" }));
    await screen.findByText(CHECKOUT_STORAGE_UNAVAILABLE);
    expect(stub.calls).toHaveLength(0);
    expect(screen.queryByRole("button", { name: "Proceed to checkout" })).toBeNull();
    expect(navigations).toEqual([]);
  });

  test("an explicit replay 404 without a code is ineligible and keeps the revision-6 guard", async () => {
    await writeIntentRows([intentRow({ scope: SCOPE_A, basketRevision: 4 })]);
    const stub = createCheckoutGatewayStub();
    const navigations: string[] = [];
    const view = render(controls({ revision: 4, populated: true }, stub, navigations));
    await waitForReady(view.container);
    fireEvent.click(screen.getByRole("button", { name: "Retry checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(1);
    });
    await act(async () => {
      stub.respond(0, problemResult(404));
    });
    await screen.findByText(UNRESOLVED);
    expect(screen.queryByText(CHECKOUT_RECOVERY_UNCONFIRMED)).toBeNull();
    view.unmount();

    const newer = render(controls({ revision: 6, populated: true }, stub, navigations));
    await waitForReady(newer.container);
    const proceed = screen.getByRole("button", {
      name: "Proceed to checkout",
    }) as HTMLButtonElement;
    expect(proceed.disabled).toBe(true);
    fireEvent.click(proceed);
    expect(stub.calls).toHaveLength(1);
    expect(screen.queryByRole("button", { name: "Set aside previous checkout" })).toBeNull();
    expect(screen.getByRole("button", { name: "Retry checkout" })).toBeTruthy();
    expect(await readIntentRow(SCOPE_A, 4)).not.toBeNull();
    expect(await readIntentRow(SCOPE_A, 6)).toBeNull();
  });

  test("an explicit replay 404 with a wrong code is ineligible and keeps the revision-6 guard", async () => {
    await writeIntentRows([intentRow({ scope: SCOPE_A, basketRevision: 4 })]);
    const stub = createCheckoutGatewayStub();
    const navigations: string[] = [];
    const view = render(controls({ revision: 4, populated: true }, stub, navigations));
    await waitForReady(view.container);
    fireEvent.click(screen.getByRole("button", { name: "Retry checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(1);
    });
    await act(async () => {
      stub.respond(0, problemResult(404, "invalid_request"));
    });
    await screen.findByText(UNRESOLVED);
    view.unmount();

    const newer = render(controls({ revision: 6, populated: true }, stub, navigations));
    await waitForReady(newer.container);
    const proceed = screen.getByRole("button", {
      name: "Proceed to checkout",
    }) as HTMLButtonElement;
    expect(proceed.disabled).toBe(true);
    fireEvent.click(proceed);
    expect(stub.calls).toHaveLength(1);
    expect(screen.queryByRole("button", { name: "Set aside previous checkout" })).toBeNull();
    const row = await readIntentRow(SCOPE_A, 4);
    expect(row?.recoveryState).toBeNull();
  });

  test("multiple unresolved rows are listed in descending original revision order", async () => {
    await writeIntentRows([
      intentRow({
        scope: SCOPE_A,
        basketRevision: 4,
        orderId: ORDER_O,
        recoveryState: "notFound",
      }),
      intentRow({
        scope: SCOPE_A,
        basketRevision: 6,
        idempotencyKey: KEY_B,
        stateFence: FENCE_B,
      }),
    ]);
    const stub = createCheckoutGatewayStub();
    const navigations: string[] = [];
    const view = render(controls({ revision: 8, populated: false }, stub, navigations));
    await waitForReady(view.container);
    const labels = screen.getAllByText(/^Basket revision /).map((element) => element.textContent);
    expect(labels).toEqual(["Basket revision 6", "Basket revision 4"]);
    expect(screen.getAllByRole("button", { name: "Retry checkout" })).toHaveLength(2);
    expect(navigations).toEqual([]);
  });

  test("a bound notFound row remains retryable from the basket with its original tuple", async () => {
    await writeIntentRows([
      intentRow({
        scope: SCOPE_A,
        basketRevision: 4,
        orderId: ORDER_O,
        recoveryState: "notFound",
      }),
    ]);
    const before = await readIntentRow(SCOPE_A, 4);
    if (!before) {
      throw new Error("Expected a seeded checkout intent.");
    }
    const stub = createCheckoutGatewayStub();
    const navigations: string[] = [];
    const view = render(controls({ revision: 5, populated: false }, stub, navigations));
    await waitForReady(view.container);
    await screen.findByText(CHECKOUT_RECOVERY_UNCONFIRMED);
    expect(screen.queryByRole("button", { name: "Set aside previous checkout" })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Retry checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(1);
    });
    expect(stub.calls[0]).toEqual({
      scope: SCOPE_A,
      basketRevision: 4,
      idempotencyKey: before.idempotencyKey,
    });
    await act(async () => {
      stub.respond(0, problemResult(404, "not_found"));
    });
    await waitFor(async () => {
      const row = await readIntentRow(SCOPE_A, 4);
      expect(row?.orderId).toBe(ORDER_O);
      expect(row?.recoveryState).toBe("notFound");
      expect(row?.stateFence).toBe(before.stateFence);
    });
    expect(screen.queryByRole("button", { name: "Set aside previous checkout" })).toBeNull();
    expect(navigations).toEqual([]);
  });

  test("a bound resume 404 preserves the order ID and key and offers only basket and history navigation", async () => {
    await writeIntentRows([intentRow({ scope: SCOPE_A, basketRevision: 4, orderId: ORDER_O })]);
    const stub = createCheckoutGatewayStub();
    const navigations: string[] = [];
    const view = render(
      <CheckoutControls
        navigate={(url) => {
          navigations.push(url);
        }}
        order={{ id: ORDER_O, status: "pendingPayment" }}
        scope={SCOPE_A}
        submit={stub.submit}
      />,
    );
    await waitForReady(view.container);
    const before = await readIntentRow(SCOPE_A, 4);
    fireEvent.click(screen.getByRole("button", { name: "Resume checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(1);
    });
    await act(async () => {
      stub.respond(0, problemResult(404, "not_found"));
    });
    await screen.findByText(CHECKOUT_RECOVERY_UNCONFIRMED);
    expect(screen.getByRole("link", { name: "Back to basket" }).getAttribute("href")).toBe(
      "/basket",
    );
    expect(screen.getByRole("button", { name: "Resume checkout" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Set aside previous checkout" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Retry checkout" })).toBeNull();
    expect(navigations).toEqual([]);
    const after = await readIntentRow(SCOPE_A, 4);
    expect(after?.orderId).toBe(ORDER_O);
    expect(after?.idempotencyKey).toBe(before?.idempotencyKey);
    expect(after?.recoveryState).toBe("notFound");
    expect(after?.stateFence).toBe(before?.stateFence);
  });
});
