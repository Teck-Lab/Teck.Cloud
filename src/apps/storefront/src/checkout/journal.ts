import type { CheckoutResult } from "./types";

export const CHECKOUT_DB_NAME = "teck-storefront-commerce-v1";
export const CHECKOUT_STORE_NAME = "checkout-intents";

const BASKET_REVISION_MIN = 1;
const BASKET_REVISION_MAX = 4_294_967_295;
const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/u;
const ROW_KEYS = [
  "basketRevision",
  "idempotencyKey",
  "orderId",
  "recoveryState",
  "scope",
  "stateFence",
] as const;
const KNOWN_ORDER_STATUS: Record<string, true> = {
  pendingPayment: true,
  pendingApproval: true,
  pendingReadiness: true,
  active: true,
  rejected: true,
  failed: true,
};
const ERROR_STATUSES: readonly number[] = [400, 401, 403, 404, 409, 422, 503];

export type CheckoutRecoveryState = null | "notFound" | "parkedAfterNotFound";
export type CheckoutAttempt = "initial" | "replay";

export interface CheckoutIntentRow {
  scope: string;
  basketRevision: number;
  idempotencyKey: string;
  orderId: string | null;
  recoveryState: CheckoutRecoveryState;
  stateFence: string;
}

export interface CapturedCheckoutIntent {
  scope: string;
  basketRevision: number;
  idempotencyKey: string;
  orderId: string | null;
  stateFence: string;
  attempt: CheckoutAttempt;
}

export type BeginCheckoutOutcome =
  | {
      status: "ready";
      attempt: CheckoutAttempt;
      intent: CapturedCheckoutIntent;
    }
  | { status: "blocked"; blockedBy: CheckoutIntentRow }
  | { status: "unavailable" };

export type CheckoutRetainedReason =
  | "denied"
  | "unauthorized"
  | "forbidden"
  | "conflict"
  | "unavailable"
  | "malformed"
  | "unresolved";

export type CheckoutApplyOutcome =
  | { kind: "bound"; orderId: string; checkoutUrl: string | null }
  | { kind: "removed"; orderId: string | null }
  | { kind: "notFound" }
  | { kind: "retained"; reason: CheckoutRetainedReason }
  | { kind: "stale" }
  | { kind: "unavailable" };

export type CheckoutSetAsideOutcome = "parked" | "stale";

export class CheckoutJournalUnavailableError extends Error {
  constructor() {
    super("Checkout journal is unavailable.");
    this.name = "CheckoutJournalUnavailableError";
  }
}

export class CheckoutJournalInvalidRowError extends Error {
  constructor() {
    super("Checkout journal row is invalid.");
    this.name = "CheckoutJournalInvalidRowError";
  }
}

function isBasketRevision(value: unknown): value is number {
  return (
    typeof value === "number" &&
    Number.isSafeInteger(value) &&
    value >= BASKET_REVISION_MIN &&
    value <= BASKET_REVISION_MAX
  );
}

function assertScope(scope: unknown): asserts scope is string {
  if (typeof scope !== "string" || scope.length === 0) {
    throw new CheckoutJournalUnavailableError();
  }
}

function assertBasketRevision(revision: unknown): asserts revision is number {
  if (!isBasketRevision(revision)) {
    throw new CheckoutJournalUnavailableError();
  }
}

function assertUuid(value: unknown): asserts value is string {
  if (typeof value !== "string" || !UUID_PATTERN.test(value)) {
    throw new CheckoutJournalUnavailableError();
  }
}

function parseRow(value: unknown): CheckoutIntentRow | null {
  if (value === null || value === undefined) {
    return null;
  }
  if (typeof value !== "object" || Array.isArray(value)) {
    throw new CheckoutJournalInvalidRowError();
  }
  const record = value as Record<string, unknown>;
  const keys = Object.keys(record).sort();
  if (keys.length !== ROW_KEYS.length || keys.some((key, index) => key !== ROW_KEYS[index])) {
    throw new CheckoutJournalInvalidRowError();
  }
  const { scope, basketRevision, idempotencyKey, orderId, recoveryState, stateFence } = record;
  assertRowScope(scope);
  if (!isBasketRevision(basketRevision)) {
    throw new CheckoutJournalInvalidRowError();
  }
  if (typeof idempotencyKey !== "string" || !UUID_PATTERN.test(idempotencyKey)) {
    throw new CheckoutJournalInvalidRowError();
  }
  if (orderId !== null && (typeof orderId !== "string" || !UUID_PATTERN.test(orderId))) {
    throw new CheckoutJournalInvalidRowError();
  }
  if (
    recoveryState !== null &&
    recoveryState !== "notFound" &&
    recoveryState !== "parkedAfterNotFound"
  ) {
    throw new CheckoutJournalInvalidRowError();
  }
  if (typeof stateFence !== "string" || !UUID_PATTERN.test(stateFence)) {
    throw new CheckoutJournalInvalidRowError();
  }
  if (orderId !== null && recoveryState === "parkedAfterNotFound") {
    throw new CheckoutJournalInvalidRowError();
  }
  return {
    scope,
    basketRevision,
    idempotencyKey,
    orderId,
    recoveryState,
    stateFence,
  };
}

function assertRowScope(scope: unknown): asserts scope is string {
  if (typeof scope !== "string" || scope.length === 0) {
    throw new CheckoutJournalInvalidRowError();
  }
}

function createUuid(): string {
  const source = globalThis.crypto;
  if (!source || typeof source.randomUUID !== "function") {
    throw new CheckoutJournalUnavailableError();
  }
  return source.randomUUID();
}

function openDatabase(): Promise<IDBDatabase> {
  const factory = globalThis.indexedDB;
  if (!factory) {
    return Promise.reject(new CheckoutJournalUnavailableError());
  }
  const { promise, resolve, reject } = Promise.withResolvers<IDBDatabase>();
  let request: IDBOpenDBRequest | null = null;
  try {
    request = factory.open(CHECKOUT_DB_NAME, 1);
  } catch {
    request = null;
  }
  if (!request) {
    reject(new CheckoutJournalUnavailableError());
    return promise;
  }
  request.onupgradeneeded = () => {
    const database = request.result;
    if (!database.objectStoreNames.contains(CHECKOUT_STORE_NAME)) {
      database.createObjectStore(CHECKOUT_STORE_NAME, {
        keyPath: ["scope", "basketRevision"],
      });
    }
  };
  request.onsuccess = () => {
    resolve(request.result);
  };
  request.onerror = () => {
    reject(new CheckoutJournalUnavailableError());
  };
  request.onblocked = () => {
    reject(new CheckoutJournalUnavailableError());
  };
  return promise;
}

function requestResult<T>(request: IDBRequest<T>): Promise<T> {
  const { promise, resolve, reject } = Promise.withResolvers<T>();
  request.onsuccess = () => {
    resolve(request.result);
  };
  request.onerror = () => {
    reject(request.error ?? new CheckoutJournalUnavailableError());
  };
  return promise;
}

function transactionCommitted(transaction: IDBTransaction): Promise<void> {
  const { promise, resolve, reject } = Promise.withResolvers<void>();
  transaction.oncomplete = () => {
    resolve();
  };
  transaction.onerror = () => {
    reject(transaction.error ?? new CheckoutJournalUnavailableError());
  };
  transaction.onabort = () => {
    reject(transaction.error ?? new CheckoutJournalUnavailableError());
  };
  return promise;
}

async function withTransaction<T>(
  mode: IDBTransactionMode,
  callback: (store: IDBObjectStore) => Promise<T>,
): Promise<T> {
  const database = await openDatabase();
  try {
    const transaction = database.transaction(CHECKOUT_STORE_NAME, mode);
    const committed = transactionCommitted(transaction);
    let result: T;
    try {
      result = await callback(transaction.objectStore(CHECKOUT_STORE_NAME));
    } catch (error) {
      committed.catch(() => {});
      throw error;
    }
    await committed;
    return result;
  } finally {
    database.close();
  }
}

function scopeKeyRange(scope: string): IDBKeyRange {
  const keyRangeFactory = globalThis.IDBKeyRange;
  if (!keyRangeFactory) {
    throw new CheckoutJournalUnavailableError();
  }
  return keyRangeFactory.bound([scope, 0], [scope, BASKET_REVISION_MAX]);
}

async function readScopeRows(store: IDBObjectStore, scope: string): Promise<CheckoutIntentRow[]> {
  const values = await requestResult(store.getAll(scopeKeyRange(scope)));
  const rows: CheckoutIntentRow[] = [];
  for (const value of values) {
    const row = parseRow(value);
    if (row && row.scope === scope) {
      rows.push(row);
    }
  }
  return rows;
}

export function captureCheckoutIntent(
  row: CheckoutIntentRow,
  attempt: CheckoutAttempt,
): CapturedCheckoutIntent {
  return {
    scope: row.scope,
    basketRevision: row.basketRevision,
    idempotencyKey: row.idempotencyKey,
    orderId: row.orderId,
    stateFence: row.stateFence,
    attempt,
  };
}

export async function listCheckoutIntents(scope: string): Promise<CheckoutIntentRow[]> {
  assertScope(scope);
  const rows = await withTransaction("readonly", (store) => readScopeRows(store, scope));
  return rows.sort((left, right) => right.basketRevision - left.basketRevision);
}

export async function beginCheckoutIntent(
  scope: string,
  basketRevision: number,
): Promise<BeginCheckoutOutcome> {
  assertScope(scope);
  assertBasketRevision(basketRevision);
  return withTransaction<BeginCheckoutOutcome>("readwrite", async (store) => {
    const existing = parseRow(await requestResult(store.get([scope, basketRevision])));
    if (existing) {
      return {
        status: "ready",
        attempt: "replay",
        intent: captureCheckoutIntent(existing, "replay"),
      };
    }
    const rows = await readScopeRows(store, scope);
    const blockedBy = rows.find(
      (row) =>
        row.orderId === null &&
        !(row.recoveryState === "parkedAfterNotFound" && basketRevision > row.basketRevision),
    );
    if (blockedBy) {
      return { status: "blocked", blockedBy };
    }
    const created: CheckoutIntentRow = {
      scope,
      basketRevision,
      idempotencyKey: createUuid(),
      orderId: null,
      recoveryState: null,
      stateFence: createUuid(),
    };
    await requestResult(store.put(created));
    return {
      status: "ready",
      attempt: "initial",
      intent: captureCheckoutIntent(created, "initial"),
    };
  });
}

const LOOPBACK_HOSTS: readonly string[] = ["localhost", "127.0.0.1", "[::1]", "::1"];

function isCheckoutRedirectUrl(value: string): boolean {
  if (value.length === 0) {
    return false;
  }
  try {
    const url = new URL(value);
    if (url.hostname.length === 0 || url.username.length > 0 || url.password.length > 0) {
      return false;
    }
    if (url.protocol === "https:") {
      return true;
    }
    return url.protocol === "http:" && LOOPBACK_HOSTS.includes(url.hostname);
  } catch {
    return false;
  }
}

interface ParsedCheckoutResult {
  status: number;
  orderId: string | null;
  orderStatus: string | null;
  checkoutUrl: string | null;
  checkoutDenied: boolean;
  code: string | null;
}

function parseCheckoutResult(result: CheckoutResult): ParsedCheckoutResult | null {
  const status = result.status;
  if (typeof status !== "number" || !Number.isInteger(status)) {
    return null;
  }
  if (status === 201) {
    const order: unknown = result.order;
    if (!order || typeof order !== "object") {
      return null;
    }
    const orderId: unknown = "id" in order ? order.id : undefined;
    const orderStatus: unknown = "status" in order ? order.status : undefined;
    if (typeof orderId !== "string" || !UUID_PATTERN.test(orderId)) {
      return null;
    }
    if (typeof orderStatus !== "string" || !Object.hasOwn(KNOWN_ORDER_STATUS, orderStatus)) {
      return null;
    }
    const denied: unknown = result.checkoutDenied;
    if (denied !== undefined && typeof denied !== "boolean") {
      return null;
    }
    const checkoutUrl = result.checkoutUrl;
    if (checkoutUrl !== null && typeof checkoutUrl !== "string") {
      return null;
    }
    if (denied !== true && checkoutUrl !== null && !isCheckoutRedirectUrl(checkoutUrl)) {
      return null;
    }
    return {
      status,
      orderId,
      orderStatus,
      checkoutUrl,
      checkoutDenied: denied === true,
      code: null,
    };
  }
  if (!ERROR_STATUSES.includes(status)) {
    return null;
  }
  const code =
    "code" in result && typeof result.code === "string" && result.code.length > 0
      ? result.code
      : null;
  return {
    status,
    orderId: null,
    orderStatus: null,
    checkoutUrl: null,
    checkoutDenied: false,
    code,
  };
}

function matchesCaptured(row: CheckoutIntentRow, captured: CapturedCheckoutIntent): boolean {
  return (
    row.scope === captured.scope &&
    row.basketRevision === captured.basketRevision &&
    row.idempotencyKey === captured.idempotencyKey &&
    row.orderId === captured.orderId &&
    row.stateFence === captured.stateFence
  );
}

export async function applyCheckoutResult(
  captured: CapturedCheckoutIntent,
  result: CheckoutResult,
): Promise<CheckoutApplyOutcome> {
  const parsed = parseCheckoutResult(result);
  try {
    return await withTransaction<CheckoutApplyOutcome>("readwrite", async (store) => {
      let live: CheckoutIntentRow | null;
      try {
        live = parseRow(await requestResult(store.get([captured.scope, captured.basketRevision])));
      } catch {
        return { kind: "unavailable" };
      }
      if (!live || !matchesCaptured(live, captured)) {
        return { kind: "stale" };
      }
      if (!parsed) {
        return { kind: "retained", reason: "malformed" };
      }
      if (parsed.status === 201) {
        if (parsed.orderId === null) {
          return { kind: "retained", reason: "malformed" };
        }
        if (parsed.checkoutDenied) {
          return { kind: "retained", reason: "denied" };
        }
        if (captured.orderId !== null && parsed.orderId !== captured.orderId) {
          return { kind: "retained", reason: "malformed" };
        }
        if (parsed.orderStatus !== "pendingPayment") {
          await requestResult(store.delete([live.scope, live.basketRevision]));
          return { kind: "removed", orderId: parsed.orderId };
        }
        const next: CheckoutIntentRow = {
          ...live,
          orderId: parsed.orderId,
          recoveryState: null,
          stateFence: createUuid(),
        };
        await requestResult(store.put(next));
        return { kind: "bound", orderId: parsed.orderId, checkoutUrl: parsed.checkoutUrl };
      }
      if (parsed.status === 404) {
        if (captured.attempt !== "replay" || parsed.code !== "not_found") {
          return { kind: "retained", reason: "unresolved" };
        }
        if (live.recoveryState === "parkedAfterNotFound") {
          return { kind: "retained", reason: "unresolved" };
        }
        if (live.recoveryState !== "notFound") {
          await requestResult(store.put({ ...live, recoveryState: "notFound" }));
        }
        return { kind: "notFound" };
      }
      if (parsed.status === 409) {
        if (
          captured.orderId === null &&
          (parsed.code === "stale_revision" || parsed.code === "invalid_transition")
        ) {
          await requestResult(store.delete([live.scope, live.basketRevision]));
          return { kind: "removed", orderId: null };
        }
        return { kind: "retained", reason: "conflict" };
      }
      if (parsed.status === 401) {
        return { kind: "retained", reason: "unauthorized" };
      }
      if (parsed.status === 403) {
        return { kind: "retained", reason: "forbidden" };
      }
      return { kind: "retained", reason: "unavailable" };
    });
  } catch {
    return { kind: "unavailable" };
  }
}

export async function parkCheckoutIntent(
  captured: CapturedCheckoutIntent,
): Promise<CheckoutSetAsideOutcome> {
  assertScope(captured.scope);
  assertBasketRevision(captured.basketRevision);
  return withTransaction<CheckoutSetAsideOutcome>("readwrite", async (store) => {
    let live: CheckoutIntentRow | null;
    try {
      live = parseRow(await requestResult(store.get([captured.scope, captured.basketRevision])));
    } catch {
      throw new CheckoutJournalUnavailableError();
    }
    if (
      !live ||
      live.idempotencyKey !== captured.idempotencyKey ||
      live.orderId !== null ||
      live.recoveryState !== "notFound" ||
      live.stateFence !== captured.stateFence
    ) {
      return "stale";
    }
    await requestResult(
      store.put({
        ...live,
        recoveryState: "parkedAfterNotFound",
        stateFence: createUuid(),
      }),
    );
    return "parked";
  });
}

export async function forgetCheckoutIntentsForOrder(scope: string, orderId: string): Promise<void> {
  assertScope(scope);
  assertUuid(orderId);
  await withTransaction("readwrite", async (store) => {
    const rows = await readScopeRows(store, scope);
    for (const row of rows) {
      if (row.orderId === orderId) {
        await requestResult(store.delete([row.scope, row.basketRevision]));
      }
    }
  });
}
