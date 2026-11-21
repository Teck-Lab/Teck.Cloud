import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

const source = await readFile(new URL("./src/api.ts", import.meta.url), "utf8");
const javascript = ts.transpileModule(source, {
  compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext },
}).outputText;
const api = await import(
  `data:text/javascript;base64,${Buffer.from(javascript).toString("base64")}`
);

test("customer requests use server catalog/options and only send chosen IDs", async () => {
  const calls = [];
  const client = api.createApiClient({
    baseUrl: "https://gateway.example/",
    getAccessToken: () => "session-token",
    fetcher: async (url, init) => {
      calls.push({ url: String(url), init });
      return Response.json({ items: [] });
    },
  });

  await client.listOfferings();
  await client.listOptions();
  await client.createOrder("off_database", "opt_small", "idempotency-123456");

  assert.equal(calls[0].url, "https://gateway.example/v1/dedicated-database/offerings");
  assert.equal(calls[1].url, "https://gateway.example/v1/dedicated-database/options");
  assert.equal(calls[2].init.method, "POST");
  assert.equal(calls[2].init.headers.get("Authorization"), "Bearer session-token");
  assert.equal(calls[2].init.headers.get("Idempotency-Key"), "idempotency-123456");
  assert.deepEqual(JSON.parse(calls[2].init.body), {
    offeringId: "off_database",
    optionId: "opt_small",
  });
  assert.doesNotMatch(calls[2].init.body, /tenant|price|credentials/i);
});

test("admin diagnostics and actions use distinct routes and never expose customer scope", async () => {
  const calls = [];
  const client = api.createApiClient({
    baseUrl: "https://gateway.example",
    getAccessToken: () => null,
    fetcher: async (url, init) => {
      calls.push({ url: String(url), init });
      return Response.json({});
    },
  });
  await client.getAdminDiagnostics("ord_1");
  await client.adminAction("ord_1", "hold", "idempotency-123456", {
    expectedRevision: "5",
    reason: "operator request",
  });

  assert.equal(
    calls[0].url,
    "https://gateway.example/v1/admin/dedicated-database/orders/ord_1/diagnostics",
  );
  assert.equal(
    calls[1].url,
    "https://gateway.example/v1/admin/dedicated-database/orders/ord_1/actions/hold",
  );
  assert.equal(calls[1].init.headers.get("Idempotency-Key"), "idempotency-123456");
  assert.deepEqual(JSON.parse(calls[1].init.body), {
    expectedRevision: "5",
    reason: "operator request",
  });
  assert.doesNotMatch(calls[1].init.body, /tenantId|connectionString|secret/i);
});

test("decimal revision comparison preserves precision and rejects malformed values", () => {
  assert.equal(api.newerRevision("9007199254740992", "9007199254740993"), true);
  assert.equal(api.newerRevision("9007199254740993", "9007199254740992"), false);
  assert.equal(api.newerRevision("01", "2"), false);
});

test("structured API errors preserve status and safe detail", async () => {
  const client = api.createApiClient({
    baseUrl: "https://gateway.example",
    getAccessToken: () => null,
    fetcher: async () => Response.json({ detail: "Not visible", status: 404 }, { status: 404 }),
  });
  await assert.rejects(
    client.listOrders(),
    (error) =>
      error instanceof api.ApiError && error.status === 404 && error.message === "Not visible",
  );
});

test("idempotency keys use browser cryptographic UUID generation", () => {
  const original = globalThis.crypto;
  Object.defineProperty(globalThis, "crypto", {
    configurable: true,
    value: { randomUUID: () => "uuid-123" },
  });
  try {
    assert.equal(api.idempotencyKey(), "uuid-123");
  } finally {
    Object.defineProperty(globalThis, "crypto", { configurable: true, value: original });
  }
});

test("SignalR subscription negotiates, handshakes, then requests the authorized snapshot", async () => {
  const sockets = [];
  const calls = [];
  const states = [];
  const changes = [];
  const timers = new Map();
  let timerId = 0;
  const runtime = {
    negotiate: async (url, token) => {
      calls.push([url.pathname, token]);
      return new URL("wss://gateway.example/hub?id=connection-token");
    },
    createSocket: () => {
      const socket = {
        readyState: 1,
        sent: [],
        send(frame) {
          this.sent.push(frame);
        },
        close() {
          this.readyState = 3;
          this.onclose?.();
        },
        onopen: null,
        onmessage: null,
        onclose: null,
        onerror: null,
      };
      sockets.push(socket);
      return socket;
    },
    setTimeout: (callback, delay) => {
      const id = ++timerId;
      timers.set(id, { callback, delay });
      return id;
    },
    clearTimeout: (id) => timers.delete(id),
    setInterval: () => ++timerId,
    clearInterval: () => {},
    random: () => 0,
  };
  const subscription = new api.OperationSubscription(
    "https://gateway.example",
    () => "access-token",
    "ord_visible",
    (hint) => changes.push(hint),
    (state) => states.push(state),
    runtime,
  );
  subscription.watchOperation("op_current");
  subscription.start();
  await new Promise((resolve) => setImmediate(resolve));
  const socket = sockets[0];
  socket.onopen();
  assert.equal(socket.sent[0], `${JSON.stringify({ protocol: "json", version: 1 })}\u001e`);
  assert.deepEqual(changes, []);
  socket.onmessage({ data: "{}\u001e" });
  const subscribe = JSON.parse(socket.sent[1].slice(0, -1));
  assert.deepEqual(subscribe, {
    type: 1,
    invocationId: "1",
    target: "SubscribeToOrder",
    arguments: [{ orderId: "ord_visible" }],
  });
  assert.deepEqual(changes, []);
  socket.onmessage({ data: `${JSON.stringify({ type: 3, invocationId: "1" })}\u001e` });
  assert.deepEqual(changes, [null]);
  socket.onmessage({
    data: `${JSON.stringify({ type: 1, target: "OperationChanged", arguments: [{ operationId: "op_current", revision: "9007199254740993" }] })}\u001e`,
  });
  assert.deepEqual(changes[1], { operationId: "op_current", revision: "9007199254740993" });
  assert.deepEqual(calls, [["/hubs/dedicated-database-operations", "access-token"]]);
  subscription.stop();
});

test("failed SignalR subscription never requests a snapshot and terminal visibility stops watching", async () => {
  const sockets = [];
  const changes = [];
  const states = [];
  const timers = new Map();
  let timerId = 0;
  const runtime = {
    negotiate: async () => new URL("wss://gateway.example/hub?id=connection-token"),
    createSocket: () => {
      const socket = {
        readyState: 1,
        sent: [],
        send(frame) {
          this.sent.push(frame);
        },
        close() {
          this.readyState = 3;
          this.onclose?.();
        },
        onopen: null,
        onmessage: null,
        onclose: null,
        onerror: null,
      };
      sockets.push(socket);
      return socket;
    },
    setTimeout: (callback, delay) => {
      const id = ++timerId;
      timers.set(id, { callback, delay });
      return id;
    },
    clearTimeout: (id) => timers.delete(id),
    setInterval: () => ++timerId,
    clearInterval: () => {},
    random: () => 0,
  };
  const subscription = new api.OperationSubscription(
    "https://gateway.example",
    () => null,
    "ord_hidden",
    (hint) => changes.push(hint),
    (state) => states.push(state),
    runtime,
  );
  subscription.watchOperation("op_hidden");
  subscription.start();
  await new Promise((resolve) => setImmediate(resolve));
  const socket = sockets[0];
  socket.onopen();
  socket.onmessage({ data: "{}\u001e" });
  socket.onmessage({
    data: `${JSON.stringify({ type: 3, invocationId: "1", error: "SERVICE_UNAVAILABLE" })}\u001e`,
  });
  assert.deepEqual(changes, []);
  assert.equal(socket.readyState, 3);
  assert.equal([...timers.values()][0].delay, 500);
  subscription.stop();

  const terminal = new api.OperationSubscription(
    "https://gateway.example",
    () => null,
    "ord_hidden",
    (hint) => changes.push(hint),
    (state) => states.push(state),
    runtime,
  );
  terminal.watchOperation("op_hidden");
  terminal.start();
  await new Promise((resolve) => setImmediate(resolve));
  const hiddenSocket = sockets[1];
  hiddenSocket.onopen();
  hiddenSocket.onmessage({ data: "{}\u001e" });
  hiddenSocket.onmessage({
    data: `${JSON.stringify({ type: 3, invocationId: "1", error: "RESOURCE_NOT_VISIBLE" })}\u001e`,
  });
  assert.equal(states.at(-1), "not-visible");
  assert.deepEqual(changes, []);
});
