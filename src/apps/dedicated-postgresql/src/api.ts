export type Price = { amountMinor: number; currency: string; interval: string };
export type Offering = { offeringId: string; displayName: string; description?: string | null; kind: string; optionIds: string[]; includedCapabilities: Record<string, unknown>; selectableEntitlements: { entitlementId: string; capability: string }[] };
export type Option = { optionId: string; offeringId: string; displayName: string; region?: string; storageGiB?: number; price: Price };
export type Order = { orderId: string; status: string; paymentStatus: string; offeringId: string; optionId: string; priceSnapshot: Price & { capturedAt: string }; createdAt: string; updatedAt: string; placementSummary: unknown; entitlementSummary: { status: string; validFrom?: string | null; validUntil?: string | null }; operationId?: string | null; [key: string]: unknown };
export type Operation = { operationId: string; orderId: string; revision: string; status: string; phase: string; requiredStepCount: number; succeededStepCount: number; failedStepCount: number; steps: { stepId: string; status: string; failedCode?: string | null }[]; updatedAt: string; [key: string]: unknown };
export type AdminOrder = Order & { failureSummary?: unknown; lastOperationStatus?: string | null };
export type Diagnostic = { code: string; severity: string; occurredAt: string; message: string };
export type Diagnostics = { orderId: string; items: Diagnostic[] };
export type AdminOperation = Operation & { diagnostics?: Diagnostic[]; manualException?: { exceptionId: string; status: string; evidenceUrl: string; activationRequired: boolean; blockedReasons?: string[] } | null };
export type RealtimeUpdate = { operationId: string; revision: string };

export function parseRealtimeFrame(frame: string): RealtimeUpdate | null {
  try {
    const message: unknown = JSON.parse(frame);
    if (typeof message !== "object" || message === null || !("type" in message) || message.type !== 1 || !("target" in message) || message.target !== "OperationChanged" || !("arguments" in message) || !Array.isArray(message.arguments)) return null;
    const update = message.arguments[0];
    if (typeof update !== "object" || update === null || !("operationId" in update) || typeof update.operationId !== "string" || !/^op_[A-Za-z0-9_-]+$/.test(update.operationId) || !("revision" in update) || typeof update.revision !== "string" || !/^(0|[1-9]\d*)$/.test(update.revision)) return null;
    return { operationId: update.operationId, revision: update.revision };
  } catch {
    return null;
  }
}
type Page<T> = { items: T[]; nextCursor?: string | null };
export type ClientConfig = { baseUrl: string; getAccessToken: () => string | null | Promise<string | null>; fetcher?: typeof fetch };
export class ApiError extends Error {
  constructor(public readonly status: number, message: string, public readonly problem?: unknown) { super(message); this.name = "ApiError"; }
}

export function createApiClient(config: ClientConfig) {
  const fetcher = config.fetcher ?? fetch;
  async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const token = await config.getAccessToken();
    const headers = new Headers(init.headers);
    headers.set("Accept", "application/json");
    if (init.body) headers.set("Content-Type", "application/json");
    if (token) headers.set("Authorization", `Bearer ${token}`);
    const response = await fetcher(new URL(path, config.baseUrl), { ...init, headers });
    if (!response.ok) {
      let problem: unknown;
      try { problem = await response.json(); } catch { /* Non-JSON error response. */ }
      const detail = typeof problem === "object" && problem !== null && "detail" in problem && typeof problem.detail === "string" ? problem.detail : `Request failed (${response.status})`;
      throw new ApiError(response.status, detail, problem);
    }
    if (response.status === 204) return undefined as T;
    return response.json() as Promise<T>;
  }
  const customer = "/v1/dedicated-database";
  const admin = "/v1/admin/dedicated-database";
  return {
    listOfferings: () => request<{ items: Offering[] }>(`${customer}/offerings`),
    listOptions: () => request<{ items: Option[] }>(`${customer}/options`),
    listOrders: (cursor?: string) => request<Page<Order>>(`${customer}/orders${cursor ? `?cursor=${encodeURIComponent(cursor)}` : ""}`),
    createOrder: (offeringId: string, optionId: string, key: string) => request<Order>(`${customer}/orders`, { method: "POST", headers: { "Idempotency-Key": key }, body: JSON.stringify({ offeringId, optionId }) }),
    getOrder: (id: string) => request<Order>(`${customer}/orders/${encodeURIComponent(id)}`),
    getOperation: (id: string) => request<Operation>(`${customer}/operations/${encodeURIComponent(id)}`),
    listAdminOrders: (cursor?: string) => request<Page<AdminOrder>>(`${admin}/orders${cursor ? `?cursor=${encodeURIComponent(cursor)}` : ""}`),
    getAdminOrder: (id: string) => request<AdminOrder>(`${admin}/orders/${encodeURIComponent(id)}`),
    getAdminDiagnostics: (id: string) => request<Diagnostics>(`${admin}/orders/${encodeURIComponent(id)}/diagnostics`),
    getAdminOperation: (id: string) => request<AdminOperation>(`${admin}/operations/${encodeURIComponent(id)}`),
    adminAction: (id: string, action: "retry" | "restore" | "hold" | "refund", key: string, body: Record<string, unknown>) => request<{ orderId: string; operationId: string; auditRecordId: string; revision: string; acceptedAt: string }>(`${admin}/orders/${encodeURIComponent(id)}/actions/${action}`, { method: "POST", headers: { "Idempotency-Key": key }, body: JSON.stringify(body) }),
  };
}
export type SignalRWebSocket = Pick<WebSocket, "readyState" | "send" | "close"> & { onopen: (() => void) | null; onmessage: ((event: MessageEvent<string>) => void) | null; onclose: (() => void) | null; onerror: (() => void) | null };
export type SignalRRuntime = { negotiate: (url: URL, token: string | null) => Promise<URL>; createSocket: (url: URL) => SignalRWebSocket; setTimeout: (callback: () => void, delay: number) => number; clearTimeout: (id: number) => void; setInterval: (callback: () => void, delay: number) => number; clearInterval: (id: number) => void; random: () => number };
export const SIGNALR_RECORD_SEPARATOR = "\u001e";

export async function negotiateSignalR(url: URL, token: string | null, fetcher: typeof fetch = fetch): Promise<URL> {
  const negotiateUrl = new URL(url);
  negotiateUrl.protocol = negotiateUrl.protocol === "wss:" ? "https:" : "http:";
  negotiateUrl.pathname = `${negotiateUrl.pathname.replace(/\/$/, "")}/negotiate`;
  negotiateUrl.searchParams.set("negotiateVersion", "1");
  const response = await fetcher(negotiateUrl, { method: "POST", headers: token ? { Authorization: `Bearer ${token}` } : undefined });
  if (!response.ok) throw new Error(`SignalR negotiation failed (${response.status}).`);
  const negotiation = await response.json() as { connectionToken?: string; url?: string; accessToken?: string };
  if (!negotiation.connectionToken || !negotiation.url) throw new Error("SignalR negotiation response is missing its connection token or URL.");
  const socketUrl = new URL(negotiation.url);
  socketUrl.protocol = socketUrl.protocol === "https:" ? "wss:" : socketUrl.protocol === "http:" ? "ws:" : socketUrl.protocol;
  socketUrl.searchParams.set("id", negotiation.connectionToken);
  const accessToken = negotiation.accessToken ?? token;
  if (accessToken) socketUrl.searchParams.set("access_token", accessToken);
  return socketUrl;
}

export type SignalRState = "connecting" | "connected" | "disconnected" | "unavailable" | "not-visible" | "stopped";
export class OperationSubscription {
  private socket: SignalRWebSocket | null = null;
  private closed = false;
  private subscribed = false;
  private handshaken = false;
  private invocationCounter = 0;
  private invocationId = "";
  private retry = 0;
  private retryTimer: number | null = null;
  private pingTimer: number | null = null;
  private buffer = "";
  private operationId = "";
  constructor(private readonly baseUrl: string, private readonly token: () => string | null | Promise<string | null>, private readonly orderId: string, private readonly changed: (hint: RealtimeUpdate | null) => void, private readonly state: (state: SignalRState) => void, private readonly runtime: SignalRRuntime = browserSignalRRuntime) {}
  start() { this.closed = false; this.subscribed = false; this.state("connecting"); void this.connect(); }

  stop(finalState: SignalRState = "stopped") {
    this.closed = true;
    if (this.retryTimer !== null) this.runtime.clearTimeout(this.retryTimer);
    if (this.pingTimer !== null) this.runtime.clearInterval(this.pingTimer);
    this.retryTimer = null;
    this.pingTimer = null;
    this.socket?.close();
    this.socket = null;
    this.subscribed = false;
    this.state(finalState);
  }

  watchOperation(operationId: string) { this.operationId = operationId; }

  private async connect() {
    try {
      const token = await this.token();
      const url = await this.runtime.negotiate(new URL("/hubs/dedicated-database-operations", this.baseUrl), token);
      if (this.closed) return;
      const socket = this.runtime.createSocket(url);
      this.socket = socket;
      this.handshaken = false;
      this.buffer = "";
      socket.onopen = () => {
        this.state("connecting");
        socket.send(JSON.stringify({ protocol: "json", version: 1 }) + SIGNALR_RECORD_SEPARATOR);
      };
      socket.onmessage = (event) => this.receive(String(event.data), socket);
      socket.onerror = () => this.state("disconnected");
      socket.onclose = () => {
        if (this.pingTimer !== null) this.runtime.clearInterval(this.pingTimer);
        this.pingTimer = null;
        if (!this.closed && this.socket === socket) {
          this.socket = null;
          this.subscribed = false;
          this.state("disconnected");
          this.scheduleReconnect();
        }
      };
    } catch {
      if (!this.closed) { this.state("disconnected"); this.scheduleReconnect(); }
    }
  }

  private receive(data: string, socket: SignalRWebSocket) {
    this.buffer += data;
    const frames = this.buffer.split(SIGNALR_RECORD_SEPARATOR);
    this.buffer = frames.pop() ?? "";
    for (const frame of frames) {
      if (!frame) continue;
      let message: unknown;
      try { message = JSON.parse(frame); } catch { this.state("disconnected"); socket.close(); return; }
      if (typeof message !== "object" || message === null) { this.state("disconnected"); socket.close(); return; }
      if (!this.handshaken) {
        if (Object.keys(message).length !== 0) { this.state("disconnected"); socket.close(); return; }
        this.handshaken = true;
        this.sendSubscribe(socket);
        this.pingTimer = this.runtime.setInterval(() => { if (socket.readyState === 1) socket.send(JSON.stringify({ type: 6 }) + SIGNALR_RECORD_SEPARATOR); }, 15000);
        continue;
      }
      if (!("type" in message)) continue;
      if (message.type === 1) {
        if (!this.subscribed) continue;
        const hint = parseRealtimeFrame(frame);
        if (hint && hint.operationId === this.operationId) this.changed(hint);
        continue;
      }
      if (message.type !== 3 || !("invocationId" in message) || String(message.invocationId) !== this.invocationId) continue;
      if ("error" in message) {
        const code = typeof message.error === "string" ? message.error : "";
        this.subscribed = false;
        if (code === "RESOURCE_NOT_VISIBLE") this.stop("not-visible");
        else if (code === "SERVICE_UNAVAILABLE") {
          this.state("unavailable");
          this.socket = null;
          socket.close();
          this.scheduleReconnect();
        } else this.stop();
        continue;
      }
      if ("result" in message) { this.stop(); continue; }
      this.subscribed = true;
      this.retry = 0;
      this.state("connected");
      this.changed(null);
    }
  }

  private sendSubscribe(socket: SignalRWebSocket) {
    if (this.closed || socket.readyState !== 1) return;
    this.invocationId = String(++this.invocationCounter);
    socket.send(JSON.stringify({ type: 1, invocationId: this.invocationId, target: "SubscribeToOrder", arguments: [{ orderId: this.orderId }] }) + SIGNALR_RECORD_SEPARATOR);
  }

  private retryDelay() {
    const exponential = 500 * 2 ** Math.min(this.retry++, 6);
    return Math.min(30000, exponential + Math.floor(this.runtime.random() * 500));
  }


  private scheduleReconnect() {
    if (this.closed || this.retryTimer !== null) return;
    this.retryTimer = this.runtime.setTimeout(() => {
      this.retryTimer = null;
      void this.connect();
    }, this.retryDelay());
  }
}

const browserSignalRRuntime: SignalRRuntime = {
  negotiate: (url, token) => negotiateSignalR(url, token),
  createSocket: (url) => new WebSocket(url) as SignalRWebSocket,
  setTimeout: (callback, delay) => window.setTimeout(callback, delay),
  clearTimeout: (id) => window.clearTimeout(id),
  setInterval: (callback, delay) => window.setInterval(callback, delay),
  clearInterval: (id) => window.clearInterval(id),
  random: () => Math.random(),
};

export function newerRevision(current: string, incoming: string): boolean {
  if (!/^(0|[1-9]\d*)$/.test(current) || !/^(0|[1-9]\d*)$/.test(incoming)) return false;
  return BigInt(incoming) > BigInt(current);
}

export function idempotencyKey(): string {
  if (!globalThis.crypto?.randomUUID) throw new Error("Secure random IDs are not available in this browser.");
  return globalThis.crypto.randomUUID();
}


