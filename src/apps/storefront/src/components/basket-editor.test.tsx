import { afterEach, describe, expect, test } from "bun:test";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import type {
  BasketActionResult,
  BasketSaveInput,
  BasketView,
  OfferView,
} from "@/commerce/contracts";
import { BasketEditor } from "./basket-editor";

afterEach(cleanup);

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

const emptyBasket: BasketView = {
  selection: null,
  revision: 0,
  total: null,
};

function createSave(result: BasketActionResult) {
  const calls: BasketSaveInput[] = [];
  const save = async (input: BasketSaveInput): Promise<BasketActionResult> => {
    calls.push(input);
    return result;
  };
  return { save, calls };
}

describe("basket editor", () => {
  test("submits the gateway offer id and selected eligible add-ons", async () => {
    const { save, calls } = createSave({
      status: 200,
      basket: {
        selection: {
          offerId: "dedicated",
          addOnCodes: ["replicaReadAccess"],
        },
        revision: 1,
        total: { amountMinor: 3500, currency: "USD" },
      },
    });

    render(
      <BasketEditor catalog={catalog} basket={emptyBasket} save={save} refreshHref="/basket" />,
    );

    fireEvent.click(screen.getByRole("radio", { name: /Dedicated PostgreSQL/ }));
    fireEvent.click(screen.getByRole("checkbox", { name: /Read replica access/ }));
    fireEvent.click(screen.getByRole("button", { name: "Save selection" }));

    await waitFor(() => expect(calls).toHaveLength(1));
    expect(calls[0]).toEqual({
      selection: {
        offerId: "dedicated",
        addOnCodes: ["replicaReadAccess"],
      },
      expectedRevision: 0,
    });
    expect(await screen.findByText(/Basket saved\. Current total \$35\.00\./)).toBeTruthy();
    expect(screen.getByText(/revision 1/)).toBeTruthy();
  });

  test("clears the saved selection with the observed revision", async () => {
    const { save, calls } = createSave({
      status: 200,
      basket: { selection: null, revision: 3, total: null },
    });

    render(
      <BasketEditor
        catalog={catalog}
        basket={{
          selection: { offerId: "shared", addOnCodes: [] },
          revision: 2,
          total: { amountMinor: 1000, currency: "USD" },
        }}
        save={save}
        refreshHref="/basket"
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Clear basket" }));

    await waitFor(() => expect(calls).toHaveLength(1));
    expect(calls[0]).toEqual({ selection: null, expectedRevision: 2 });
    expect(await screen.findByText(/Basket saved\. Current total Not set\./)).toBeTruthy();
  });

  test("stops writes on a stale revision and offers a read-only refresh", async () => {
    const { save, calls } = createSave({
      status: 409,
      code: "stale_revision",
    });

    render(
      <BasketEditor
        catalog={catalog}
        basket={{
          selection: { offerId: "shared", addOnCodes: [] },
          revision: 2,
          total: { amountMinor: 1000, currency: "USD" },
        }}
        save={save}
        refreshHref="/basket"
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Save selection" }));

    expect(await screen.findByText(/changed in another tab/)).toBeTruthy();
    const refresh = screen.getByRole("link", { name: "Refresh" });
    expect(refresh.getAttribute("href")).toBe("/basket");
    const disabledSaveButton = screen.getByRole("button", {
      name: "Save selection",
    }) as HTMLButtonElement;
    expect(disabledSaveButton.disabled).toBe(true);

    fireEvent.click(disabledSaveButton);
    expect(calls).toHaveLength(1);
  });

  test("requires a catalog refresh when an option is no longer offered", async () => {
    const { save } = createSave({ status: 422, code: "unsupported_option" });

    render(
      <BasketEditor catalog={catalog} basket={emptyBasket} save={save} refreshHref="/catalog" />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Save selection" }));

    expect(await screen.findByText(/no longer offered/)).toBeTruthy();
    expect(screen.getByRole("link", { name: "Refresh" }).getAttribute("href")).toBe("/catalog");
  });

  test("announces a transport failure without clearing the form", async () => {
    const save = async (): Promise<BasketActionResult> => {
      throw new Error("offline");
    };

    render(
      <BasketEditor catalog={catalog} basket={emptyBasket} save={save} refreshHref="/basket" />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Save selection" }));

    expect(await screen.findByText(/The basket could not be saved\. Try again\./)).toBeTruthy();
    expect(
      (screen.getByRole("button", { name: "Save selection" }) as HTMLButtonElement).disabled,
    ).toBe(false);
  });
});
