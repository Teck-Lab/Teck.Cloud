import { afterEach, beforeEach, describe, expect, test } from "bun:test";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { CheckoutControlsProps } from "@/checkout/controls";
import { CHECKOUT_RECOVERY_UNCONFIRMED, CHECKOUT_SET_ASIDE_CONFIRMATION } from "@/checkout/copy";
import {
  createCheckoutGatewayStub,
  intentRow,
  KEY_A,
  ORDER_O,
  payableResult,
  problemResult,
  readIntentRow,
  SCOPE_A,
  writeIntentRows,
} from "@/checkout/testing/fixtures";
import { installMemoryIndexedDb, removeMemoryIndexedDb } from "@/checkout/testing/memory-indexeddb";
import type {
  BasketActionResult,
  BasketSaveInput,
  BasketView,
  OfferView,
} from "@/commerce/contracts";
import type { BasketSave } from "@/components/basket-editor";
import { type BasketCatalog, BasketWorkspace } from "./basket-workspace";

const TEMPORARY_UNAVAILABLE = "Checkout is temporarily unavailable. Try again.";
const BLOCKED = "An earlier checkout needs attention before you can start another one.";
const EMPTY_HINT = "Choose a plan and save it before starting checkout.";
const SET_ASIDE_DONE = "Previous checkout set aside. You can start a new checkout.";

const catalog: OfferView[] = [
  {
    offerId: "shared",
    tier: "shared",
    displayName: "Shared PostgreSQL",
    basePrice: { amountMinor: 1000, currency: "USD" },
    addOns: [],
  },
  {
    offerId: "dedicated",
    tier: "dedicated",
    displayName: "Dedicated PostgreSQL",
    basePrice: { amountMinor: 3000, currency: "USD" },
    addOns: [
      {
        code: "replicaReadAccess",
        displayName: "Read replica access",
        price: { amountMinor: 500, currency: "USD" },
      },
    ],
  },
];

const catalogView: BasketCatalog = { status: "ready", offers: catalog };

const emptyBasket: BasketView = { selection: null, revision: 0, total: null };

function selectionBasket(
  revision: number,
  offerId: string,
  addOnCodes: string[] = [],
  amountMinor = 1000,
): BasketView {
  return {
    selection: { offerId, addOnCodes },
    revision,
    total: { amountMinor, currency: "USD" },
  };
}

function createSave(results: BasketActionResult[]) {
  const calls: BasketSaveInput[] = [];
  const queue = [...results];
  const save: BasketSave = async (input) => {
    calls.push(input);
    const next = queue.shift();
    if (next === undefined) {
      throw new Error("Unexpected basket save.");
    }
    return next;
  };
  return { calls, save };
}

function setup(options: {
  basket: BasketView;
  save: BasketSave;
  submit: CheckoutControlsProps["submit"];
}) {
  const navigations: string[] = [];
  const navigate = (url: string) => {
    navigations.push(url);
  };
  const element = (basket: BasketView) => (
    <BasketWorkspace
      basket={basket}
      catalog={catalogView}
      navigate={navigate}
      refreshHref="/basket"
      save={options.save}
      scope={SCOPE_A}
      submit={options.submit}
    />
  );
  const view = render(element(options.basket));
  return {
    navigations,
    rerender: (basket: BasketView) => {
      view.rerender(element(basket));
    },
  };
}

async function waitForCheckoutReady(): Promise<void> {
  await waitFor(() => {
    const region = screen.getByRole("heading", { name: "Checkout" }).closest("section");
    expect(region?.querySelector('[aria-busy="false"]')).not.toBeNull();
  });
}

beforeEach(() => {
  installMemoryIndexedDb();
});

afterEach(() => {
  cleanup();
  removeMemoryIndexedDb();
});

describe("basket page workspace", () => {
  test("a first save from an empty basket enables checkout at the saved revision", async () => {
    const stub = createCheckoutGatewayStub();
    const save = createSave([{ status: 200, basket: selectionBasket(1, "shared") }]);
    const basketPage = setup({ basket: emptyBasket, save: save.save, submit: stub.submit });

    await waitForCheckoutReady();
    expect(screen.getByText(EMPTY_HINT)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Proceed to checkout" })).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Save selection" }));

    expect(await screen.findByText(/Basket saved\. Current total \$10\.00\./)).toBeTruthy();
    expect(screen.getByText(/revision 1/)).toBeTruthy();
    expect(screen.queryByText(EMPTY_HINT)).toBeNull();
    expect(save.calls).toEqual([
      { selection: { offerId: "shared", addOnCodes: [] }, expectedRevision: 0 },
    ]);

    const proceed = screen.getByRole("button", {
      name: "Proceed to checkout",
    }) as HTMLButtonElement;
    expect(proceed.disabled).toBe(false);
    fireEvent.click(proceed);

    await waitFor(() => {
      expect(stub.calls).toHaveLength(1);
    });
    const intent = await readIntentRow(SCOPE_A, 1);
    if (!intent) {
      throw new Error("Expected a checkout intent for revision 1.");
    }
    expect(stub.calls[0]).toEqual({
      scope: SCOPE_A,
      basketRevision: 1,
      idempotencyKey: intent.idempotencyKey,
    });

    await act(async () => {
      stub.respond(0, payableResult());
    });
    await waitFor(() => {
      expect(basketPage.navigations).toEqual([`/orders/${ORDER_O}?checkout=unavailable`]);
    });
  });

  test("edits and checkout use each newly observed revision", async () => {
    const stub = createCheckoutGatewayStub();
    const save = createSave([
      { status: 200, basket: selectionBasket(2, "dedicated", ["replicaReadAccess"], 3500) },
      { status: 200, basket: selectionBasket(3, "dedicated", [], 3000) },
    ]);
    const basketPage = setup({
      basket: selectionBasket(1, "shared"),
      save: save.save,
      submit: stub.submit,
    });
    await waitForCheckoutReady();

    fireEvent.click(screen.getByRole("radio", { name: /Dedicated PostgreSQL/ }));
    fireEvent.click(screen.getByRole("checkbox", { name: /Read replica access/ }));
    fireEvent.click(screen.getByRole("button", { name: "Save selection" }));

    expect(await screen.findByText(/revision 2/)).toBeTruthy();
    expect(save.calls[0]).toEqual({
      selection: { offerId: "dedicated", addOnCodes: ["replicaReadAccess"] },
      expectedRevision: 1,
    });

    fireEvent.click(screen.getByRole("checkbox", { name: /Read replica access/ }));
    fireEvent.click(screen.getByRole("button", { name: "Save selection" }));

    expect(await screen.findByText(/revision 3/)).toBeTruthy();
    expect(save.calls[1]).toEqual({
      selection: { offerId: "dedicated", addOnCodes: [] },
      expectedRevision: 2,
    });

    basketPage.rerender(selectionBasket(1, "shared"));
    expect(screen.getByText(/revision 3/)).toBeTruthy();
    expect(screen.queryByText(/revision 1/)).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Proceed to checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(1);
    });
    expect(stub.calls[0]?.basketRevision).toBe(3);

    await act(async () => {
      stub.respond(0, payableResult());
    });
    await waitFor(() => {
      expect(basketPage.navigations).toHaveLength(1);
    });
  });

  test("clearing removes Proceed while persisted recovery stays available", async () => {
    await writeIntentRows([
      intentRow({ scope: SCOPE_A, basketRevision: 4, recoveryState: "notFound" }),
    ]);
    const stub = createCheckoutGatewayStub();
    const save = createSave([
      { status: 200, basket: { selection: null, revision: 6, total: null } },
    ]);
    setup({
      basket: selectionBasket(5, "shared"),
      save: save.save,
      submit: stub.submit,
    });

    await waitForCheckoutReady();
    expect(await screen.findByText(CHECKOUT_RECOVERY_UNCONFIRMED)).toBeTruthy();
    expect(screen.getByRole("button", { name: "Proceed to checkout" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Set aside previous checkout" })).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Clear basket" }));

    expect(await screen.findByText(/Basket saved\. Current total Not set\./)).toBeTruthy();
    expect(screen.getByText(/revision 6/)).toBeTruthy();
    expect(screen.getByText(EMPTY_HINT)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Proceed to checkout" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Set aside previous checkout" })).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Retry checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(1);
    });
    expect(stub.calls[0]).toEqual({
      scope: SCOPE_A,
      basketRevision: 4,
      idempotencyKey: KEY_A,
    });

    await act(async () => {
      stub.respond(0, problemResult(503));
    });
    expect(await screen.findByText(TEMPORARY_UNAVAILABLE)).toBeTruthy();
  });

  test("set-aside and checkout use the newly saved authoritative revision", async () => {
    await writeIntentRows([
      intentRow({ scope: SCOPE_A, basketRevision: 4, recoveryState: "notFound" }),
    ]);
    const stub = createCheckoutGatewayStub();
    const save = createSave([{ status: 200, basket: selectionBasket(6, "dedicated", [], 3000) }]);
    const basketPage = setup({
      basket: selectionBasket(3, "shared"),
      save: save.save,
      submit: stub.submit,
    });

    await waitForCheckoutReady();
    expect(await screen.findByText(CHECKOUT_RECOVERY_UNCONFIRMED)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Set aside previous checkout" })).toBeNull();
    expect(screen.getByText(BLOCKED)).toBeTruthy();

    fireEvent.click(screen.getByRole("radio", { name: /Dedicated PostgreSQL/ }));
    fireEvent.click(screen.getByRole("button", { name: "Save selection" }));

    expect(await screen.findByText(/revision 6/)).toBeTruthy();
    expect(save.calls[0]).toEqual({
      selection: { offerId: "dedicated", addOnCodes: [] },
      expectedRevision: 3,
    });

    fireEvent.click(screen.getByRole("button", { name: "Set aside previous checkout" }));
    expect(await screen.findByText(CHECKOUT_SET_ASIDE_CONFIRMATION)).toBeTruthy();
    expect(stub.calls).toHaveLength(0);
    expect(
      (
        screen.getByRole("button", {
          name: "Proceed to checkout",
        }) as HTMLButtonElement
      ).disabled,
    ).toBe(true);

    fireEvent.click(screen.getByRole("button", { name: "Confirm set aside" }));
    expect(await screen.findByText(SET_ASIDE_DONE)).toBeTruthy();
    expect(stub.calls).toHaveLength(0);
    expect(screen.queryByText(BLOCKED)).toBeNull();
    const parked = await readIntentRow(SCOPE_A, 4);
    expect(parked?.recoveryState).toBe("parkedAfterNotFound");
    expect(parked?.idempotencyKey).toBe(KEY_A);

    const proceed = screen.getByRole("button", {
      name: "Proceed to checkout",
    }) as HTMLButtonElement;
    expect(proceed.disabled).toBe(false);
    fireEvent.click(proceed);

    await waitFor(() => {
      expect(stub.calls).toHaveLength(1);
    });
    const fresh = await readIntentRow(SCOPE_A, 6);
    if (!fresh) {
      throw new Error("Expected a checkout intent for revision 6.");
    }
    expect(stub.calls[0]).toEqual({
      scope: SCOPE_A,
      basketRevision: 6,
      idempotencyKey: fresh.idempotencyKey,
    });
    expect(stub.calls[0]?.idempotencyKey).not.toBe(KEY_A);

    await act(async () => {
      stub.respond(0, payableResult());
    });
    await waitFor(() => {
      expect(basketPage.navigations).toHaveLength(1);
    });
  });

  test("checkout is unavailable while a basket write is in flight", async () => {
    const stub = createCheckoutGatewayStub();
    const pendingSave = Promise.withResolvers<BasketActionResult>();
    const calls: BasketSaveInput[] = [];
    const save: BasketSave = (input) => {
      calls.push(input);
      return pendingSave.promise;
    };
    setup({ basket: selectionBasket(2, "shared"), save, submit: stub.submit });

    await waitForCheckoutReady();
    expect(
      (screen.getByRole("button", { name: "Proceed to checkout" }) as HTMLButtonElement).disabled,
    ).toBe(false);

    fireEvent.click(screen.getByRole("radio", { name: /Dedicated PostgreSQL/ }));
    fireEvent.click(screen.getByRole("button", { name: "Save selection" }));

    expect(calls).toEqual([
      { selection: { offerId: "dedicated", addOnCodes: [] }, expectedRevision: 2 },
    ]);
    expect(screen.queryByRole("button", { name: "Proceed to checkout" })).toBeNull();
    expect(
      (screen.getByRole("group", { name: "Checkout actions" }) as HTMLFieldSetElement).disabled,
    ).toBe(true);
    expect(
      (screen.getByRole("button", { name: "Save selection" }) as HTMLButtonElement).disabled,
    ).toBe(true);
    expect(stub.calls).toHaveLength(0);

    await act(async () => {
      pendingSave.resolve({ status: 200, basket: selectionBasket(3, "dedicated", [], 3000) });
    });
    expect(await screen.findByText(/revision 3/)).toBeTruthy();

    expect(
      (screen.getByRole("group", { name: "Checkout actions" }) as HTMLFieldSetElement).disabled,
    ).toBe(false);
    expect(
      (screen.getByRole("button", { name: "Proceed to checkout" }) as HTMLButtonElement).disabled,
    ).toBe(false);
    expect(stub.calls).toHaveLength(0);
  });

  test("a same-page snapshot refresh preserves the unsaved draft", async () => {
    const stub = createCheckoutGatewayStub();
    const save = createSave([
      { status: 200, basket: selectionBasket(2, "dedicated", ["replicaReadAccess"], 3500) },
    ]);
    const basketPage = setup({
      basket: selectionBasket(1, "shared"),
      save: save.save,
      submit: stub.submit,
    });
    await waitForCheckoutReady();

    fireEvent.click(screen.getByRole("radio", { name: /Dedicated PostgreSQL/ }));
    fireEvent.click(screen.getByRole("checkbox", { name: /Read replica access/ }));

    basketPage.rerender(selectionBasket(1, "shared"));

    expect(
      (screen.getByRole("radio", { name: /Dedicated PostgreSQL/ }) as HTMLInputElement).checked,
    ).toBe(true);
    expect(
      (screen.getByRole("checkbox", { name: /Read replica access/ }) as HTMLInputElement).checked,
    ).toBe(true);

    fireEvent.click(screen.getByRole("button", { name: "Save selection" }));
    await waitFor(() => {
      expect(save.calls).toHaveLength(1);
    });
    expect(save.calls[0]).toEqual({
      selection: { offerId: "dedicated", addOnCodes: ["replicaReadAccess"] },
      expectedRevision: 1,
    });
    expect(await screen.findByText(/Basket saved\. Current total \$35\.00\./)).toBeTruthy();
  });

  test("a newer same-page snapshot replaces a conflicted read-only view", async () => {
    const stub = createCheckoutGatewayStub();
    const save = createSave([{ status: 409, code: "stale_revision" }]);
    const basketPage = setup({
      basket: selectionBasket(1, "shared"),
      save: save.save,
      submit: stub.submit,
    });
    await waitForCheckoutReady();

    fireEvent.click(screen.getByRole("radio", { name: /Dedicated PostgreSQL/ }));
    fireEvent.click(screen.getByRole("button", { name: "Save selection" }));

    expect(await screen.findByText(/changed in another tab/)).toBeTruthy();
    expect(screen.getByRole("link", { name: "Refresh" }).getAttribute("href")).toBe("/basket");
    expect(
      (screen.getByRole("button", { name: "Save selection" }) as HTMLButtonElement).disabled,
    ).toBe(true);

    basketPage.rerender(selectionBasket(5, "dedicated", ["replicaReadAccess"], 3500));

    expect(screen.getByText(/revision 5/)).toBeTruthy();
    expect(screen.queryByText(/changed in another tab/)).toBeNull();
    expect(
      (screen.getByRole("button", { name: "Save selection" }) as HTMLButtonElement).disabled,
    ).toBe(false);
    expect(
      (screen.getByRole("radio", { name: /Dedicated PostgreSQL/ }) as HTMLInputElement).checked,
    ).toBe(true);
    expect(
      (screen.getByRole("checkbox", { name: /Read replica access/ }) as HTMLInputElement).checked,
    ).toBe(true);

    fireEvent.click(screen.getByRole("button", { name: "Proceed to checkout" }));
    await waitFor(() => {
      expect(stub.calls).toHaveLength(1);
    });
    expect(stub.calls[0]?.basketRevision).toBe(5);

    await act(async () => {
      stub.respond(0, payableResult());
    });
    await waitFor(() => {
      expect(basketPage.navigations).toHaveLength(1);
    });
  });
});
