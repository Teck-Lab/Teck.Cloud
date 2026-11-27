import { beforeEach, describe, expect, test } from "bun:test";
import {
  applyCheckoutResult,
  beginCheckoutIntent,
  type CheckoutIntentRow,
  captureCheckoutIntent,
  forgetCheckoutIntentsForOrder,
  listCheckoutIntents,
  parkCheckoutIntent,
  replayCheckoutIntent,
} from "./journal";
import {
  CHECKOUT_URL,
  clearIntentRows,
  deleteIntentRow,
  FENCE_B,
  intentRow,
  KEY_B,
  ORDER_O,
  ORDER_OTHER,
  payableResult,
  problemResult,
  readIntentRow,
  readIntentRows,
  SCOPE_A,
  SCOPE_B,
  writeIntentRows,
} from "./testing/fixtures";
import { installMemoryIndexedDb, removeMemoryIndexedDb } from "./testing/memory-indexeddb";

beforeEach(() => {
  installMemoryIndexedDb();
});

describe("checkout journal", () => {
  test("concurrent get-or-create reuses one idempotency key and one row", async () => {
    const [first, second] = await Promise.all([
      beginCheckoutIntent(SCOPE_A, 4),
      beginCheckoutIntent(SCOPE_A, 4),
    ]);
    expect(first.status).toBe("ready");
    expect(second.status).toBe("ready");
    if (first.status !== "ready" || second.status !== "ready") {
      throw new Error("Expected both checkout attempts to be ready.");
    }
    expect(first.intent.idempotencyKey).toBe(second.intent.idempotencyKey);
    expect(first.intent.stateFence).toBe(second.intent.stateFence);
    expect(new Set([first.attempt, second.attempt])).toEqual(new Set(["initial", "replay"]));
    const rows = (await readIntentRows()) as CheckoutIntentRow[];
    expect(rows).toHaveLength(1);
    expect(rows[0]?.basketRevision).toBe(4);
  });

  test("another revision is blocked until a notFound intent is parked for a higher revision", async () => {
    await writeIntentRows([
      intentRow({ scope: SCOPE_A, basketRevision: 4, recoveryState: "notFound" }),
    ]);
    const blocked = await beginCheckoutIntent(SCOPE_A, 5);
    expect(blocked.status).toBe("blocked");
    const row = await readIntentRow(SCOPE_A, 4);
    expect(row).not.toBeNull();
    if (!row) {
      return;
    }
    const parked = await parkCheckoutIntent(captureCheckoutIntent(row, "replay"));
    expect(parked).toBe("parked");
    expect(await beginCheckoutIntent(SCOPE_A, 3)).toMatchObject({ status: "blocked" });
    expect(await beginCheckoutIntent(SCOPE_A, 5)).toMatchObject({
      status: "ready",
      attempt: "initial",
    });
  });

  test("a bound intent never blocks a different revision", async () => {
    await writeIntentRows([
      intentRow({
        scope: SCOPE_A,
        basketRevision: 4,
        orderId: ORDER_O,
      }),
    ]);
    expect(await beginCheckoutIntent(SCOPE_A, 5)).toMatchObject({
      status: "ready",
      attempt: "initial",
    });
  });

  test("another scope is invisible and never blocks", async () => {
    await writeIntentRows([
      intentRow({
        scope: SCOPE_B,
        basketRevision: 4,
        orderId: ORDER_O,
      }),
    ]);
    expect(await listCheckoutIntents(SCOPE_A)).toEqual([]);
    const begun = await beginCheckoutIntent(SCOPE_A, 5);
    expect(begun.status).toBe("ready");
    expect(await listCheckoutIntents(SCOPE_A)).toHaveLength(1);
    expect(await listCheckoutIntents(SCOPE_B)).toHaveLength(1);
  });

  test("invalid or unavailable storage fails closed", async () => {
    await writeIntentRows([
      {
        scope: SCOPE_A,
        basketRevision: 4,
        idempotencyKey: "not-a-uuid",
        orderId: null,
        recoveryState: null,
        stateFence: FENCE_B,
      },
    ]);
    await expect(listCheckoutIntents(SCOPE_A)).rejects.toThrow();
    await expect(beginCheckoutIntent(SCOPE_A, 4)).rejects.toThrow();
  });

  test("a 201 binds the order, clears recovery and rotates the fence without storing the URL", async () => {
    const begun = await beginCheckoutIntent(SCOPE_A, 4);
    if (begun.status !== "ready") {
      throw new Error("Expected a created checkout intent.");
    }
    const outcome = await applyCheckoutResult(
      begun.intent,
      payableResult({ checkoutUrl: CHECKOUT_URL }),
    );
    expect(outcome).toEqual({
      kind: "bound",
      orderId: ORDER_O,
      checkoutUrl: CHECKOUT_URL,
    });
    const row = await readIntentRow(SCOPE_A, 4);
    expect(row).not.toBeNull();
    if (!row) {
      return;
    }
    expect(row.orderId).toBe(ORDER_O);
    expect(row.recoveryState).toBeNull();
    expect(row.idempotencyKey).toBe(begun.intent.idempotencyKey);
    expect(row.stateFence).not.toBe(begun.intent.stateFence);
    expect(Object.keys(row).sort()).toEqual([
      "basketRevision",
      "idempotencyKey",
      "orderId",
      "recoveryState",
      "scope",
      "stateFence",
    ]);
    expect(JSON.stringify(await readIntentRows())).not.toContain("stripe");
  });

  test("a stale fence cannot bind, recover or delete a successor", async () => {
    const begun = await beginCheckoutIntent(SCOPE_A, 4);
    if (begun.status !== "ready") {
      throw new Error("Expected a created checkout intent.");
    }
    await writeIntentRows([
      intentRow({
        scope: SCOPE_A,
        basketRevision: 4,
        idempotencyKey: begun.intent.idempotencyKey,
        stateFence: FENCE_B,
      }),
    ]);
    expect(await applyCheckoutResult(begun.intent, payableResult())).toEqual({
      kind: "stale",
    });
    expect(await applyCheckoutResult(begun.intent, problemResult(409, "stale_revision"))).toEqual({
      kind: "stale",
    });
    const row = await readIntentRow(SCOPE_A, 4);
    expect(row?.stateFence).toBe(FENCE_B);
    expect(row?.orderId).toBeNull();
  });

  test("a result for a missing row is stale even when the result is malformed", async () => {
    const begun = await beginCheckoutIntent(SCOPE_A, 4);
    if (begun.status !== "ready") {
      throw new Error("Expected a created checkout intent.");
    }
    await clearIntentRows();
    expect(
      await applyCheckoutResult(begun.intent, {
        status: 201,
        order: { id: "not-a-uuid", status: "pendingPayment" },
        checkoutUrl: null,
      }),
    ).toEqual({ kind: "stale" });
    expect(await readIntentRows()).toEqual([]);
  });

  test("a mismatched 201 keeps the bound record unchanged", async () => {
    await writeIntentRows([intentRow({ scope: SCOPE_A, basketRevision: 4, orderId: ORDER_O })]);
    const begun = await beginCheckoutIntent(SCOPE_A, 4);
    if (begun.status !== "ready") {
      throw new Error("Expected a replayable checkout intent.");
    }
    expect(
      await applyCheckoutResult(begun.intent, payableResult({ orderId: ORDER_OTHER })),
    ).toEqual({ kind: "retained", reason: "malformed" });
    const row = await readIntentRow(SCOPE_A, 4);
    expect(row?.orderId).toBe(ORDER_O);
    expect(row?.stateFence).toBe(begun.intent.stateFence);
  });

  test("an initial 404 never enters scoped recovery", async () => {
    const begun = await beginCheckoutIntent(SCOPE_A, 4);
    if (begun.status !== "ready") {
      throw new Error("Expected a created checkout intent.");
    }
    expect(await applyCheckoutResult(begun.intent, problemResult(404, "not_found"))).toEqual({
      kind: "retained",
      reason: "unresolved",
    });
    const row = await readIntentRow(SCOPE_A, 4);
    expect(row?.recoveryState).toBeNull();
    expect(row?.stateFence).toBe(begun.intent.stateFence);
  });

  test("a replay 404 without the not_found code never enters scoped recovery", async () => {
    await writeIntentRows([intentRow({ scope: SCOPE_A, basketRevision: 4 })]);
    const begun = await beginCheckoutIntent(SCOPE_A, 4);
    if (begun.status !== "ready") {
      throw new Error("Expected a replayable checkout intent.");
    }
    expect(await applyCheckoutResult(begun.intent, problemResult(404))).toEqual({
      kind: "retained",
      reason: "unresolved",
    });
    expect(await applyCheckoutResult(begun.intent, problemResult(404, "invalid_request"))).toEqual({
      kind: "retained",
      reason: "unresolved",
    });
    expect((await readIntentRow(SCOPE_A, 4))?.recoveryState).toBeNull();
  });

  test("an eligible replay 404 marks notFound without rotating the fence; a matching 201 clears it", async () => {
    await writeIntentRows([intentRow({ scope: SCOPE_A, basketRevision: 4 })]);
    const replay = await beginCheckoutIntent(SCOPE_A, 4);
    if (replay.status !== "ready") {
      throw new Error("Expected a replayable checkout intent.");
    }
    expect(await applyCheckoutResult(replay.intent, problemResult(404, "not_found"))).toEqual({
      kind: "notFound",
    });
    const recovered = await readIntentRow(SCOPE_A, 4);
    expect(recovered?.recoveryState).toBe("notFound");
    expect(recovered?.stateFence).toBe(replay.intent.stateFence);

    const retry = await beginCheckoutIntent(SCOPE_A, 4);
    if (retry.status !== "ready") {
      throw new Error("Expected a replayable checkout intent.");
    }
    expect(await applyCheckoutResult(retry.intent, payableResult())).toEqual({
      kind: "bound",
      orderId: ORDER_O,
      checkoutUrl: null,
    });
    const bound = await readIntentRow(SCOPE_A, 4);
    expect(bound?.recoveryState).toBeNull();
    expect(bound?.orderId).toBe(ORDER_O);
    expect(bound?.stateFence).not.toBe(replay.intent.stateFence);
  });

  test("a 404 that commits before a matching 201 leaves the later 201 authoritative", async () => {
    await writeIntentRows([intentRow({ scope: SCOPE_A, basketRevision: 4 })]);
    const first = await beginCheckoutIntent(SCOPE_A, 4);
    const second = await beginCheckoutIntent(SCOPE_A, 4);
    if (first.status !== "ready" || second.status !== "ready") {
      throw new Error("Expected two replayable checkout intents.");
    }
    expect(await applyCheckoutResult(second.intent, problemResult(404, "not_found"))).toEqual({
      kind: "notFound",
    });
    expect(await applyCheckoutResult(first.intent, payableResult())).toEqual({
      kind: "bound",
      orderId: ORDER_O,
      checkoutUrl: null,
    });
    expect((await readIntentRow(SCOPE_A, 4))?.recoveryState).toBeNull();
  });

  test("a 201 that commits first makes an older 404 stale", async () => {
    await writeIntentRows([intentRow({ scope: SCOPE_A, basketRevision: 4 })]);
    const first = await beginCheckoutIntent(SCOPE_A, 4);
    const second = await beginCheckoutIntent(SCOPE_A, 4);
    if (first.status !== "ready" || second.status !== "ready") {
      throw new Error("Expected two replayable checkout intents.");
    }
    expect(await applyCheckoutResult(second.intent, payableResult())).toEqual({
      kind: "bound",
      orderId: ORDER_O,
      checkoutUrl: null,
    });
    expect(await applyCheckoutResult(first.intent, problemResult(404, "not_found"))).toEqual({
      kind: "stale",
    });
    const row = await readIntentRow(SCOPE_A, 4);
    expect(row?.recoveryState).toBeNull();
    expect(row?.orderId).toBe(ORDER_O);
  });

  test("a nonpayable 201 deletes the matching unbound row", async () => {
    const begun = await beginCheckoutIntent(SCOPE_A, 4);
    if (begun.status !== "ready") {
      throw new Error("Expected a created checkout intent.");
    }
    expect(await applyCheckoutResult(begun.intent, payableResult({ status: "active" }))).toEqual({
      kind: "removed",
      orderId: ORDER_O,
    });
    expect(await readIntentRow(SCOPE_A, 4)).toBeNull();
  });

  test("definitive unbound 409 responses delete only the matching row", async () => {
    const stale = await beginCheckoutIntent(SCOPE_A, 4);
    if (stale.status !== "ready") {
      throw new Error("Expected a created checkout intent.");
    }
    expect(await applyCheckoutResult(stale.intent, problemResult(409, "stale_revision"))).toEqual({
      kind: "removed",
      orderId: null,
    });
    expect(await readIntentRow(SCOPE_A, 4)).toBeNull();

    const invalid = await beginCheckoutIntent(SCOPE_A, 5);
    if (invalid.status !== "ready") {
      throw new Error("Expected a created checkout intent.");
    }
    expect(
      await applyCheckoutResult(invalid.intent, problemResult(409, "invalid_transition")),
    ).toEqual({ kind: "removed", orderId: null });
    expect(await readIntentRow(SCOPE_A, 5)).toBeNull();
  });

  test("authorization, unavailability and malformed results never enter recovery", async () => {
    await writeIntentRows([intentRow({ scope: SCOPE_A, basketRevision: 4 })]);
    const begun = await beginCheckoutIntent(SCOPE_A, 4);
    if (begun.status !== "ready") {
      throw new Error("Expected a replayable checkout intent.");
    }
    expect(await applyCheckoutResult(begun.intent, problemResult(401))).toEqual({
      kind: "retained",
      reason: "unauthorized",
    });
    expect(await applyCheckoutResult(begun.intent, problemResult(403))).toEqual({
      kind: "retained",
      reason: "forbidden",
    });
    expect(await applyCheckoutResult(begun.intent, problemResult(503))).toEqual({
      kind: "retained",
      reason: "unavailable",
    });
    expect(await applyCheckoutResult(begun.intent, problemResult(422))).toEqual({
      kind: "retained",
      reason: "unavailable",
    });
    for (const checkoutUrl of [
      "not-a-url",
      "http://payments.example/c/pay/1",
      "https://user:pass@checkout.stripe.com/c/pay/1",
    ]) {
      expect(
        await applyCheckoutResult(begun.intent, {
          status: 201,
          order: { id: ORDER_O, status: "pendingPayment" },
          checkoutUrl,
        }),
      ).toEqual({ kind: "retained", reason: "malformed" });
    }
    expect(
      await applyCheckoutResult(
        begun.intent,
        payableResult({ checkoutDenied: true, checkoutUrl: "https://provider.example/pay" }),
      ),
    ).toEqual({ kind: "retained", reason: "denied" });
    const row = await readIntentRow(SCOPE_A, 4);
    expect(row?.orderId).toBeNull();
    expect(row?.recoveryState).toBeNull();
    expect(row?.stateFence).toBe(begun.intent.stateFence);
  });

  test("a bound intent survives 409 responses and idempotency conflicts", async () => {
    await writeIntentRows([intentRow({ scope: SCOPE_A, basketRevision: 4, orderId: ORDER_O })]);
    const begun = await beginCheckoutIntent(SCOPE_A, 4);
    if (begun.status !== "ready") {
      throw new Error("Expected a replayable checkout intent.");
    }
    expect(await applyCheckoutResult(begun.intent, problemResult(409, "stale_revision"))).toEqual({
      kind: "retained",
      reason: "conflict",
    });
    expect(
      await applyCheckoutResult(begun.intent, problemResult(409, "idempotency_conflict")),
    ).toEqual({ kind: "retained", reason: "conflict" });
    expect((await readIntentRow(SCOPE_A, 4))?.orderId).toBe(ORDER_O);
  });

  test("set-aside requires a live unbound notFound record and rotates the fence", async () => {
    await writeIntentRows([
      intentRow({ scope: SCOPE_A, basketRevision: 4, recoveryState: "notFound" }),
    ]);
    const begun = await beginCheckoutIntent(SCOPE_A, 4);
    if (begun.status !== "ready") {
      throw new Error("Expected a replayable checkout intent.");
    }
    expect(await parkCheckoutIntent(begun.intent)).toBe("parked");
    const parked = await readIntentRow(SCOPE_A, 4);
    expect(parked?.recoveryState).toBe("parkedAfterNotFound");
    expect(parked?.stateFence).not.toBe(begun.intent.stateFence);
    expect(await parkCheckoutIntent(begun.intent)).toBe("stale");
  });

  test("set-aside never applies to a bound record", async () => {
    await writeIntentRows([
      intentRow({
        scope: SCOPE_A,
        basketRevision: 4,
        orderId: ORDER_O,
        recoveryState: "notFound",
      }),
    ]);
    const begun = await beginCheckoutIntent(SCOPE_A, 4);
    if (begun.status !== "ready") {
      throw new Error("Expected a replayable checkout intent.");
    }
    expect(await parkCheckoutIntent(begun.intent)).toBe("stale");
    const row = await readIntentRow(SCOPE_A, 4);
    expect(row?.recoveryState).toBe("notFound");
    expect(row?.orderId).toBe(ORDER_O);
  });

  test("paid cleanup removes only the scoped rows bound to that order", async () => {
    await writeIntentRows([
      intentRow({ scope: SCOPE_A, basketRevision: 4, orderId: ORDER_O }),
      intentRow({
        scope: SCOPE_A,
        basketRevision: 5,
        idempotencyKey: KEY_B,
        orderId: ORDER_OTHER,
      }),
      intentRow({
        scope: SCOPE_A,
        basketRevision: 6,
        idempotencyKey: KEY_B,
      }),
      intentRow({
        scope: SCOPE_B,
        basketRevision: 4,
        idempotencyKey: KEY_B,
        orderId: ORDER_O,
      }),
    ]);
    await forgetCheckoutIntentsForOrder(SCOPE_A, ORDER_O);
    expect(await readIntentRow(SCOPE_A, 4)).toBeNull();
    expect(await readIntentRow(SCOPE_A, 5)).not.toBeNull();
    expect(await readIntentRow(SCOPE_A, 6)).not.toBeNull();
    expect(await readIntentRow(SCOPE_B, 4)).not.toBeNull();
  });

  test("a replay reuses the displayed tuple only while the live row matches it", async () => {
    const displayed = intentRow({ scope: SCOPE_A, basketRevision: 4 });
    await writeIntentRows([displayed]);
    expect(await replayCheckoutIntent(displayed)).toEqual({
      status: "ready",
      intent: captureCheckoutIntent(displayed, "replay"),
    });
    expect(await readIntentRows()).toEqual([displayed]);
  });

  test("a removed displayed row refuses replay without allocating a key", async () => {
    const displayed = intentRow({ scope: SCOPE_A, basketRevision: 4 });
    await writeIntentRows([displayed]);
    await deleteIntentRow(SCOPE_A, 4);
    expect(await replayCheckoutIntent(displayed)).toEqual({ status: "stale" });
    expect(await readIntentRows()).toEqual([]);
  });

  test("a replaced displayed row refuses replay and leaves the replacement untouched", async () => {
    const displayed = intentRow({ scope: SCOPE_A, basketRevision: 4 });
    const replacement = intentRow({
      scope: SCOPE_A,
      basketRevision: 4,
      idempotencyKey: KEY_B,
      stateFence: FENCE_B,
    });
    await writeIntentRows([replacement]);
    expect(await replayCheckoutIntent(displayed)).toEqual({ status: "stale" });
    expect(await readIntentRows()).toEqual([replacement]);
  });

  test("a changed order binding or recovery state refuses replay", async () => {
    const unbound = intentRow({ scope: SCOPE_A, basketRevision: 4 });
    await writeIntentRows([
      intentRow({ scope: SCOPE_A, basketRevision: 4, recoveryState: "notFound" }),
    ]);
    expect(await replayCheckoutIntent(unbound)).toEqual({ status: "stale" });

    const bound = intentRow({ scope: SCOPE_A, basketRevision: 4, orderId: ORDER_O });
    await writeIntentRows([bound]);
    expect(
      await replayCheckoutIntent(
        intentRow({ scope: SCOPE_A, basketRevision: 4, orderId: ORDER_OTHER }),
      ),
    ).toEqual({ status: "stale" });
    expect(await readIntentRows()).toEqual([bound]);
  });

  test("notFound and parked rows remain explicitly replayable without journal changes", async () => {
    const notFound = intentRow({ scope: SCOPE_A, basketRevision: 4, recoveryState: "notFound" });
    await writeIntentRows([notFound]);
    expect(await replayCheckoutIntent(notFound)).toEqual({
      status: "ready",
      intent: captureCheckoutIntent(notFound, "replay"),
    });
    const parked = intentRow({
      scope: SCOPE_A,
      basketRevision: 4,
      recoveryState: "parkedAfterNotFound",
    });
    await writeIntentRows([parked]);
    expect(await replayCheckoutIntent(parked)).toEqual({
      status: "ready",
      intent: captureCheckoutIntent(parked, "replay"),
    });
    expect(await readIntentRows()).toEqual([parked]);
  });

  test("a replay after a matching 201 rotates the fence and refuses the displayed row", async () => {
    const displayed = intentRow({ scope: SCOPE_A, basketRevision: 4 });
    await writeIntentRows([displayed]);
    const replay = await replayCheckoutIntent(displayed);
    if (replay.status !== "ready") {
      throw new Error("Expected a replayable checkout intent.");
    }
    expect(await applyCheckoutResult(replay.intent, payableResult())).toMatchObject({
      kind: "bound",
      orderId: ORDER_O,
    });
    expect(await replayCheckoutIntent(displayed)).toEqual({ status: "stale" });
    expect((await readIntentRow(SCOPE_A, 4))?.orderId).toBe(ORDER_O);
  });

  test("unavailable storage or an invalid displayed row refuses replay", async () => {
    const displayed = intentRow({ scope: SCOPE_A, basketRevision: 4 });
    await writeIntentRows([displayed]);
    expect(await replayCheckoutIntent({ ...displayed, idempotencyKey: "not-a-uuid" })).toEqual({
      status: "unavailable",
    });
    removeMemoryIndexedDb();
    expect(await replayCheckoutIntent(displayed)).toEqual({ status: "unavailable" });
  });

  test("clearIntentRows resets seeded fixture data", async () => {
    await writeIntentRows([intentRow({ scope: SCOPE_A, basketRevision: 4 })]);
    await clearIntentRows();
    expect(await readIntentRows()).toEqual([]);
  });
});
