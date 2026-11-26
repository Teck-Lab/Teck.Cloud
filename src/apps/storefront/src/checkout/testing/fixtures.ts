import {
  CHECKOUT_DB_NAME,
  CHECKOUT_STORE_NAME,
  type CheckoutIntentRow,
  type CheckoutRecoveryState,
} from "../journal";
import type { CheckoutResult } from "../types";

export const SCOPE_A = "scope-alpha-opaque";
export const SCOPE_B = "scope-bravo-opaque";

export const KEY_A = "11111111-1111-4111-8111-111111111111";
export const KEY_B = "22222222-2222-4222-8222-222222222222";
export const FENCE_A = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
export const FENCE_B = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";

export const ORDER_O = "3a4b5c6d-7e8f-4a2b-9c3d-4e5f60718293";
export const ORDER_OTHER = "0f0e0d0c-0b0a-4999-8877-665544332211";
export const CHECKOUT_URL = "https://checkout.stripe.com/c/pay/cs_test_fixture";

export interface SubmitInput {
  scope: string;
  basketRevision: number;
  idempotencyKey: string;
}

export interface Deferred<T> {
  promise: Promise<T>;
  resolve: (value: T) => void;
  reject: (reason?: unknown) => void;
}

export function deferred<T>(): Deferred<T> {
  return Promise.withResolvers<T>();
}

export interface CheckoutGatewayStub {
  calls: SubmitInput[];
  submit: (input: SubmitInput) => Promise<CheckoutResult>;
  respond: (index: number, result: CheckoutResult) => void;
  fail: (index: number, reason?: unknown) => void;
}

export function createCheckoutGatewayStub(): CheckoutGatewayStub {
  const calls: SubmitInput[] = [];
  const pending: Deferred<CheckoutResult>[] = [];
  const take = (index: number): Deferred<CheckoutResult> => {
    const wait = pending[index];
    if (!wait) {
      throw new Error(`No pending checkout request at index ${index}.`);
    }
    return wait;
  };
  return {
    calls,
    submit(input) {
      calls.push(input);
      const wait = deferred<CheckoutResult>();
      pending.push(wait);
      return wait.promise;
    },
    respond(index, result) {
      take(index).resolve(result);
    },
    fail(index, reason) {
      take(index).reject(reason ?? new Error("checkout transport failed"));
    },
  };
}

export function intentRow(input: {
  scope: string;
  basketRevision: number;
  idempotencyKey?: string;
  orderId?: string | null;
  recoveryState?: CheckoutRecoveryState;
  stateFence?: string;
}): CheckoutIntentRow {
  return {
    scope: input.scope,
    basketRevision: input.basketRevision,
    idempotencyKey: input.idempotencyKey ?? KEY_A,
    orderId: input.orderId ?? null,
    recoveryState: input.recoveryState ?? null,
    stateFence: input.stateFence ?? FENCE_A,
  };
}

export function payableResult(overrides?: {
  checkoutUrl?: string | null;
  checkoutDenied?: boolean;
  orderId?: string;
  status?: string;
}): CheckoutResult {
  return {
    status: 201,
    order: {
      id: overrides?.orderId ?? ORDER_O,
      status: overrides?.status ?? "pendingPayment",
    },
    checkoutUrl: overrides?.checkoutUrl === undefined ? null : overrides.checkoutUrl,
    checkoutDenied: overrides?.checkoutDenied,
  };
}

export function problemResult(status: number, code?: string): CheckoutResult {
  return code === undefined ? { status } : { status, code };
}

function openIntentDatabase(): Promise<IDBDatabase> {
  const { promise, resolve, reject } = Promise.withResolvers<IDBDatabase>();
  const request = globalThis.indexedDB.open(CHECKOUT_DB_NAME, 1);
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
    reject(request.error ?? new Error("Unable to open checkout journal."));
  };
  request.onblocked = () => {
    reject(new Error("Checkout journal upgrade blocked."));
  };
  return promise;
}

async function withStore<T>(
  mode: IDBTransactionMode,
  callback: (store: IDBObjectStore) => Promise<T>,
): Promise<T> {
  const database = await openIntentDatabase();
  try {
    const transaction = database.transaction(CHECKOUT_STORE_NAME, mode);
    const completed = Promise.withResolvers<void>();
    transaction.oncomplete = () => {
      completed.resolve();
    };
    transaction.onerror = () => {
      completed.reject(transaction.error ?? new Error("Checkout journal transaction failed."));
    };
    transaction.onabort = () => {
      completed.reject(transaction.error ?? new Error("Checkout journal transaction aborted."));
    };
    const result = await callback(transaction.objectStore(CHECKOUT_STORE_NAME));
    await completed.promise;
    return result;
  } finally {
    database.close();
  }
}

export async function writeIntentRows(rows: readonly unknown[]): Promise<void> {
  await withStore("readwrite", async (store) => {
    for (const row of rows) {
      const { promise, resolve, reject } = Promise.withResolvers<void>();
      const request = store.put(row);
      request.onsuccess = () => {
        resolve();
      };
      request.onerror = () => {
        reject(request.error ?? new Error("Unable to seed checkout journal row."));
      };
      await promise;
    }
  });
}

export async function readIntentRows(): Promise<unknown[]> {
  return withStore("readonly", (store) => {
    const { promise, resolve, reject } = Promise.withResolvers<unknown[]>();
    const request = store.getAll();
    request.onsuccess = () => {
      resolve(request.result as unknown[]);
    };
    request.onerror = () => {
      reject(request.error ?? new Error("Unable to read checkout journal rows."));
    };
    return promise;
  });
}

export async function readIntentRow(
  scope: string,
  basketRevision: number,
): Promise<CheckoutIntentRow | null> {
  const rows = (await readIntentRows()) as CheckoutIntentRow[];
  return rows.find((row) => row.scope === scope && row.basketRevision === basketRevision) ?? null;
}

export async function clearIntentRows(): Promise<void> {
  await withStore("readwrite", (store) => {
    const { promise, resolve, reject } = Promise.withResolvers<void>();
    const request = store.clear();
    request.onsuccess = () => {
      resolve();
    };
    request.onerror = () => {
      reject(request.error ?? new Error("Unable to clear checkout journal."));
    };
    return promise;
  });
}
