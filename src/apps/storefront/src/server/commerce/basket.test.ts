import { describe, expect, test } from "bun:test";
import { performBasketSave, validateBasketSaveInput } from "./basket";
import {
  CommerceHttpError,
  CommerceInvalidResponseError,
  CommerceUnavailableError,
} from "./problems";

const basket = {
  selection: { offerId: "postgres-dedicated-monthly", addOnCodes: ["replicaReadAccess"] },
  revision: 3,
  total: { amountMinor: 3500, currency: "USD" },
};

describe("basket input", () => {
  test("accepts creating a first selection at revision zero", () => {
    const input = {
      selection: {
        offerId: "postgres-shared-monthly",
        addOnCodes: ["replicaReadAccess"],
      },
      expectedRevision: 0,
    };
    expect(validateBasketSaveInput(input)).toEqual(input);
  });

  test("accepts clearing with a null selection", () => {
    expect(validateBasketSaveInput({ selection: null, expectedRevision: 5 })).toEqual({
      selection: null,
      expectedRevision: 5,
    });
  });

  test("rejects malformed selections, codes, duplicates and revisions", () => {
    const valid = {
      selection: { offerId: "postgres-shared-monthly", addOnCodes: [] },
      expectedRevision: 1,
    };
    for (const candidate of [
      null,
      [],
      { ...valid, expectedRevision: -1 },
      { ...valid, expectedRevision: 1.5 },
      { ...valid, expectedRevision: 4294967296 },
      { selection: { offerId: "", addOnCodes: [] }, expectedRevision: 1 },
      {
        selection: { offerId: "postgres-shared-monthly", addOnCodes: ["bad code"] },
        expectedRevision: 1,
      },
      {
        selection: {
          offerId: "postgres-shared-monthly",
          addOnCodes: ["replicaReadAccess", "replicaReadAccess"],
        },
        expectedRevision: 1,
      },
      {
        selection: {
          offerId: "postgres-shared-monthly",
          addOnCodes: Array.from({ length: 21 }, (_, index) => `addOn${index}`),
        },
        expectedRevision: 1,
      },
    ]) {
      expect(validateBasketSaveInput(candidate)).toBeNull();
    }
  });

  test("returns the canonical server basket without gateway-only fields", async () => {
    const calls: unknown[] = [];
    const result = await performBasketSave(
      {
        putBasket: async (input) => {
          calls.push(input);
          return { ...basket, extra: "ignored" };
        },
      },
      {
        selection: {
          offerId: "postgres-dedicated-monthly",
          addOnCodes: ["replicaReadAccess"],
        },
        expectedRevision: 2,
      },
    );

    expect(calls).toEqual([
      {
        selection: {
          offerId: "postgres-dedicated-monthly",
          addOnCodes: ["replicaReadAccess"],
        },
        expectedRevision: 2,
      },
    ]);
    expect(result).toEqual({
      status: 200,
      basket: {
        selection: {
          offerId: "postgres-dedicated-monthly",
          addOnCodes: ["replicaReadAccess"],
        },
        revision: 3,
        total: { amountMinor: 3500, currency: "USD" },
      },
    });
  });

  test("maps only recognised conflict and denial problems", async () => {
    const conflict = await performBasketSave(
      {
        putBasket: async () => {
          throw new CommerceHttpError(409, {
            status: 409,
            code: "stale_revision",
          });
        },
      },
      { selection: null, expectedRevision: 2 },
    );
    expect(conflict).toEqual({ status: 409, code: "stale_revision" });

    const unsupported = await performBasketSave(
      {
        putBasket: async () => {
          throw new CommerceHttpError(422, {
            status: 422,
            code: "unsupported_option",
          });
        },
      },
      { selection: null, expectedRevision: 2 },
    );
    expect(unsupported).toEqual({ status: 422, code: "unsupported_option" });

    const malformed = await performBasketSave(
      {
        putBasket: async () => {
          throw new CommerceHttpError(409, null);
        },
      },
      { selection: null, expectedRevision: 2 },
    );
    expect(malformed).toEqual({ status: 502 });
  });

  test("fails closed on invalid or unavailable responses", async () => {
    const invalid = await performBasketSave(
      {
        putBasket: async () => {
          throw new CommerceInvalidResponseError();
        },
      },
      { selection: null, expectedRevision: 2 },
    );
    expect(invalid).toEqual({ status: 502 });

    const unavailable = await performBasketSave(
      {
        putBasket: async () => {
          throw new CommerceUnavailableError();
        },
      },
      { selection: null, expectedRevision: 2 },
    );
    expect(unavailable).toEqual({ status: 503, code: "dependency_unavailable" });
  });
});
