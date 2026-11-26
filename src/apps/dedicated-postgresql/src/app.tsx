import React, { useEffect, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import { ApiError, createApiClient, idempotencyKey, newerRevision, OperationSubscription, type AdminOperation, type AdminOrder, type Offering, type Option, type Operation, type Order, type SignalRState } from "./api";
import { adminOrders, customerOrders, diagnostics as fixtureDiagnostics, offerings as fixtureOfferings, operations as fixtureOperations, options as fixtureOptions } from "./fixtures";
import "./styles.css";
declare global { interface Window { teckSession?: { getAccessToken: () => string | null | Promise<string | null>; permissions?: string[] } } }
const apiBase = import.meta.env.VITE_DEDICATED_DATABASE_API_BASE_URL as string | undefined;
const api = apiBase ? createApiClient({ baseUrl: apiBase, getAccessToken: () => window.teckSession?.getAccessToken() ?? null }) : null;
const names = ["paymentPending", "migrationProgress", "operationComplete", "manualException"] as const;
type FixtureName = typeof names[number];
function formatMoney(price: { amountMinor: number; currency: string; interval: string }) {
  const value = new Intl.NumberFormat(undefined, { style: "currency", currency: price.currency }).format(price.amountMinor / 100);
  return `${value}${price.interval === "oneTime" ? " once" : ` / ${price.interval}`}`;
}
function label(value: string) { return value.replace(/([a-z])([A-Z])/g, "$1 $2").replace(/^./, (letter) => letter.toUpperCase()); }
function message(error: unknown) { return error instanceof ApiError ? error.message : error instanceof Error ? error.message : "Request failed"; }
function App() {
  const [mode, setMode] = useState<"customer" | "admin">("customer"), [demo, setDemo] = useState(!api), [fixture, setFixture] = useState<FixtureName>("migrationProgress");
  const [offers, setOffers] = useState<Offering[]>([]), [options, setOptions] = useState<Option[]>([]), [orders, setOrders] = useState<Order[]>([]), [adminList, setAdminList] = useState<AdminOrder[]>([]);
  const [offerId, setOfferId] = useState(""), [optionId, setOptionId] = useState(""), [order, setOrder] = useState<Order | AdminOrder | null>(null), [operation, setOperation] = useState<Operation | AdminOperation | null>(null);
  const [reason, setReason] = useState(""), [recoveryPoint, setRecoveryPoint] = useState(""), [amountMinor, setAmountMinor] = useState(""), [busy, setBusy] = useState(false), [error, setError] = useState(""), [notice, setNotice] = useState(""), [realtimeState, setRealtimeState] = useState<SignalRState>("stopped");
  const fixtureMode = demo || !api, permissions = window.teckSession?.permissions ?? [];
  const can = (name: string) => !fixtureMode && permissions.includes(name);
  const currentOptions = options.filter((item) => item.offeringId === offerId && offers.find((offering) => offering.offeringId === offerId)?.optionIds.includes(item.optionId));
  const load = async () => {
    if (fixtureMode) { setOffers(fixtureOfferings); setOptions(fixtureOptions); setOrders(customerOrders); setAdminList(adminOrders); return; }
    if (!api) return;
    if (mode === "customer") { const [catalog, listedOptions, listedOrders] = await Promise.all([api.listOfferings(), api.listOptions(), api.listOrders()]); setOffers(catalog.items); setOptions(listedOptions.items); setOrders(listedOrders.items); }
    else setAdminList((await api.listAdminOrders()).items);
  };
  useEffect(() => { setBusy(true); setError(""); void load().catch((reason: unknown) => setError(message(reason))).finally(() => setBusy(false)); }, [mode, demo]);
  useEffect(() => { if (!currentOptions.some((item) => item.optionId === optionId)) setOptionId(currentOptions[0]?.optionId ?? ""); }, [offerId, options]);
  useEffect(() => {
    if (!api || fixtureMode || mode !== "customer" || !order?.operationId) { setRealtimeState("stopped"); return; }
    const selectedOrder = order;
    let active = true;
    let ready = false;
    let inFlight = false;
    let queued = false;
    let terminal = false;
    let refreshSnapshot!: () => Promise<void>;
    refreshSnapshot = async () => {
      if (!active || terminal || !ready) return;
      if (inFlight) { queued = true; return; }
      inFlight = true;
      try {
        const snapshot = await api.getOperation(selectedOrder.operationId!);
        if (active) setOperation((current) => !current || newerRevision(current.revision, snapshot.revision) ? snapshot : current);
      } catch (reason) {
        if (active) {
          if (reason instanceof ApiError && reason.status === 404) { terminal = true; setError("Order not found."); setOrder(null); setOperation(null); }
          else setError(message(reason));
        }
      } finally {
        inFlight = false;
        if (active && queued && !terminal) { queued = false; void refreshSnapshot(); }
      }
    };
    const subscription = new OperationSubscription(apiBase!, () => window.teckSession?.getAccessToken() ?? null, selectedOrder.orderId, (hint) => {
      if (!active || terminal) return;
      ready = true;
      void refreshSnapshot();
    }, (state) => {
      setRealtimeState(state);
      if (state === "not-visible") { terminal = true; queued = false; ready = false; setError("Order not found."); setOrder(null); setOperation(null); }
      else if (state === "unavailable" || state === "stopped") { terminal = true; queued = false; ready = false; }
      else if (state === "connected") { terminal = false; }
      else if (state === "disconnected") ready = false;
    });
    subscription.watchOperation(selectedOrder.operationId!);
    subscription.start();
    return () => { active = false; subscription.stop(); };
  }, [order?.orderId, order?.operationId, mode, fixtureMode]);
  useEffect(() => {
    if (!api || fixtureMode || mode !== "admin" || !can("dedicated-database.orders.read") || !order?.operationId) return;
    const selectedOrder = order;
    let active = true;
    const poll = async () => {
      try {
        const snapshot = await api.getAdminOperation(selectedOrder.operationId!);
        if (active) setOperation((current) => !current || newerRevision(current.revision, snapshot.revision) ? snapshot : current);
      } catch (reason) { if (active) setError(message(reason)); }
    };
    void poll();
    const timer = window.setInterval(() => { void poll(); }, 15000);
    return () => { active = false; window.clearInterval(timer); };
  }, [order?.orderId, order?.operationId, mode, fixtureMode]);
  useEffect(() => {
    if (!api || fixtureMode || mode !== "customer" || realtimeState !== "disconnected" || !order?.operationId) return;
    const selectedOrder = order;
    let active = true;
    const poll = async () => {
      try {
        const snapshot = await api.getOperation(selectedOrder.operationId!);
        if (active) setOperation((current) => !current || newerRevision(current.revision, snapshot.revision) ? snapshot : current);
      } catch (reason) { if (active) setError(message(reason)); }
    };
    void poll();
    const timer = window.setInterval(() => { void poll(); }, 15000);
    return () => { active = false; window.clearInterval(timer); };
  }, [order?.orderId, order?.operationId, mode, fixtureMode, realtimeState]);
  const selectOrder = (selected: Order | AdminOrder, admin = false) => {
    const snapshot = fixtureMode && selected.operationId ? fixtureOperations.find((item) => item.operationId === selected.operationId) : null;
    setOrder(selected); setOperation(snapshot ?? null); setError(""); setRealtimeState("stopped");
    if (admin && !fixtureMode && !can("dedicated-database.orders.read")) setError("The host session does not grant dedicated-database.orders.read.");
  };
  const openOrder = (selected: Order | AdminOrder) => { void selectOrder(selected, mode === "admin"); }
  const submitOrder = async (event: React.FormEvent) => {
    event.preventDefault(); if (!api || fixtureMode) { setNotice("Fixture preview is read-only."); return; }
    setBusy(true); setError(""); try { const created = await api.createOrder(offerId, optionId, idempotencyKey()); setOrder(created); setNotice(`Order ${created.orderId} accepted · ${label(created.status)} · payment ${label(created.paymentStatus)}. No entitlement is active until server confirmation.`); await load(); } catch (reason) { setError(message(reason)); } finally { setBusy(false); }
  };
  const submitAction = async (action: "retry" | "restore" | "hold" | "refund") => {
    if (!api || !order || !operation || fixtureMode) return;
    if (!can(`dedicated-database.actions.${action}`)) { setError(`Missing permission: dedicated-database.actions.${action}`); return; }
    if (!reason.trim() || reason.length > 1000) { setError("A reason of 1–1000 characters is required."); return; }
    const body: Record<string, unknown> = { expectedRevision: operation.revision, reason: reason.trim() };
    if (action === "restore") { if (!recoveryPoint) { setError("Choose the approved recovery point in UTC."); return; } body.recoveryPoint = new Date(recoveryPoint).toISOString(); }
    setBusy(true); setError(""); try { const accepted = await api.adminAction(order.orderId, action, idempotencyKey(), body); setNotice(`${label(action)} request ${accepted.operationId} accepted. Refresh for authoritative state.`); await load(); } catch (reason) { setError(message(reason)); } finally { setBusy(false); }
  };
  const selectedOffer = offers.find((item) => item.offeringId === offerId), selectedOption = currentOptions.find((item) => item.optionId === optionId);
  const refresh = async () => {
    if (!api || !order) return;
    setBusy(true);
    try {
      const fresh = await api.getAdminOrder(order.orderId);
      setOrder(fresh);
      if (fresh.operationId) { setOperation(null); setRealtimeState("stopped"); }
    } catch (reason) { setError(message(reason)); }
    finally { setBusy(false); }
  };
  return <div className="shell"><header className="topbar"><a className="brand" href="#home"><span className="brand-mark">T</span> teck<span className="brand-light">cloud</span></a><span className="topbar-label">DATABASE SERVICES</span><span className="session-state">● {fixtureMode ? "Fixture preview" : "Host session"}</span></header><main><div className="hero"><div className="eyebrow">MANAGED DATA PLATFORM · NORTHERN EUROPE</div><h1>Your database,<br/><em>without the busywork.</em></h1><p>Resilient PostgreSQL services, with the control and clarity your team needs to build with confidence.</p></div><nav className="view-switch" aria-label="Application area"><button className={mode === "customer" ? "active" : ""} onClick={() => setMode("customer")}>Storefront</button><button className={mode === "admin" ? "active" : ""} onClick={() => setMode("admin")}>Admin · Orders</button>{api && <button onClick={() => setDemo(!demo)}>{demo ? "Connect live API" : "Preview fixtures"}</button>}</nav>{fixtureMode && <div className="fixture-banner"><b>SYNTHETIC FIXTURE DATA</b> Not connected to a live API or provider. {demo && <label>Scenario <select value={fixture} onChange={(event) => setFixture(event.target.value as FixtureName)}>{names.map((name) => <option key={name} value={name}>{label(name)}</option>)}</select></label>}</div>}{!apiBase && <div className="info-banner">Read-only preview. Configure <code>VITE_DEDICATED_DATABASE_API_BASE_URL</code>; the host must provide an authenticated session.</div>}{notice && <p className="success-banner" aria-live="polite">{notice}</p>}{error && <p role="alert" className="error-banner">{error}</p>}{busy && <p className="loading">Loading…</p>}
    {mode === "customer" ? <section className="content-grid"><div className="main-column"><div className="section-title"><div><div className="eyebrow">01 — SELECT YOUR FOUNDATION</div><h2>Choose a service</h2></div><span className="subtle">Prices and availability are server-defined</span></div><div className="offer-grid">{offers.map((item) => <button key={item.offeringId} className={`offer-card ${offerId === item.offeringId ? "selected" : ""}`} aria-pressed={offerId === item.offeringId} onClick={() => setOfferId(item.offeringId)}><span className="icon-tile">{item.kind === "dedicatedCluster" ? "◈" : "⌘"}</span><div className="eyebrow">{item.kind === "dedicatedCluster" ? "ISOLATED CLUSTER" : "DEDICATED DATABASE"}</div><h3>{item.displayName}</h3><p>{item.description ?? "Managed PostgreSQL with capabilities described by the authenticated catalog."}</p><div className="capabilities"><span>Baseline: {item.kind}</span><span>{item.selectableEntitlements.length} selectable entitlements</span><span>Backup/PITR {((item.includedCapabilities.backupRecovery as { physicalPitr?: boolean | null } | undefined)?.physicalPitr ? "confirmed" : "unconfirmed")}</span></div></button>)}</div><div className="section-title options-title"><div><div className="eyebrow">02 — CONFIGURE</div><h2>Choose your size</h2></div></div><div className="option-list">{currentOptions.map((item) => <label className="option-row" key={item.optionId}><input type="radio" name="option" checked={optionId === item.optionId} onChange={() => setOptionId(item.optionId)}/><span className="option-name"><strong>{item.displayName}</strong><small>{item.storageGiB ?? "—"} GiB · {item.region ?? "Region by service"}</small></span><span className="option-price">{formatMoney(item.price)}</span></label>)}</div><div className="section-title orders-heading"><div><div className="eyebrow">YOUR ACCOUNT</div><h2>Recent orders</h2></div></div><div className="orders-table-wrap"><table><thead><tr><th>Order</th><th>Payment</th><th>Status</th><th>Created</th><th/></tr></thead><tbody>{orders.map((item) => <tr key={item.orderId} className={order?.orderId === item.orderId ? "active-row" : ""} onClick={() => openOrder(item)}><td><strong>{item.orderId}</strong><small>{item.offeringId}</small></td><td>{label(item.paymentStatus)}</td><td><span className={`badge ${item.status}`}>{label(item.status)}</span></td><td>{new Date(item.createdAt).toLocaleDateString()}</td><td>›</td></tr>)}</tbody></table></div></div><aside className="detail-panel"><div className="panel-heading"><div><div className="eyebrow">ORDER SUMMARY</div><h3>{selectedOffer?.displayName ?? "Select a service"}</h3></div><span className="secure-pill">● Verified</span></div>{selectedOption ? <div className="selected-price"><strong>{formatMoney(selectedOption.price)}</strong><span>Prices are authoritative at checkout</span></div> : null}<p className="subtle">Workspace identity is supplied by the authenticated session; tenant identity never comes from this form.</p><button className="primary-button" disabled={!api || !selectedOffer || !selectedOption || busy} onClick={(event) => void submitOrder(event as unknown as React.FormEvent)}>Place order</button>{order ? <div className="detail-card"><div className="section-title"><h3>{order.orderId}</h3><span className={`badge ${order.status}`}>{label(order.status)}</span></div><p>Payment: {label(order.paymentStatus)}. Entitlement: {label(order.entitlementSummary.status)}.</p>{order.operationId ? <button className="secondary-button" onClick={() => void load()}>Refresh status</button> : null}</div> : null}</aside></section> : <section className="admin-layout"><aside className="admin-sidebar"><div className="section-title"><div><div className="eyebrow">OPERATIONS</div><h2>Orders</h2></div><span className="badge neutral">{adminList.length}</span></div>{adminList.map((item) => <button key={item.orderId} className={`admin-order ${order?.orderId === item.orderId ? "selected" : ""}`} onClick={() => openOrder(item)}><span><strong>{item.orderId}</strong><small>{item.offeringId}</small></span><span className={`badge ${item.status}`}>{label(item.status)}</span></button>)}</aside><div className="admin-main"><div className="section-title"><div><div className="eyebrow">ORDER DETAIL · ADMIN ONLY</div><h2>{order?.orderId ?? "Select an order"}</h2></div><button className="secondary-button" onClick={() => void refresh()} disabled={!order || busy}>Refresh</button></div>{order ? <div className="admin-grid"><div className="detail-card"><h3>Customer-visible state</h3><p>Payment: {label(order.paymentStatus)} · Entitlement: {label(order.entitlementSummary.status)}</p><p>Price snapshot: {formatMoney(order.priceSnapshot)}</p><p>Capabilities and entitlement activation remain server-authoritative.</p></div><div className="detail-card"><h3>Provisioning operation</h3>{operation ? <><p>{label(operation.status)} · {label(operation.phase)} · revision {operation.revision}</p><div className="step-list">{operation.steps.map((item) => <div className={`step ${item.status}`} key={item.stepId}><span>{item.status === "succeeded" ? "✓" : item.status === "failed" ? "!" : "○"}</span><strong>{label(item.stepId)}</strong><span>{label(item.status)}</span></div>)}</div>{(operation as AdminOperation).manualException ? <div className="warning-card"><strong>Manual exception: {label((operation as AdminOperation).manualException!.status)}</strong><p>Recovery evidence: <a href={(operation as AdminOperation).manualException!.evidenceUrl} target="_blank" rel="noreferrer">open approved evidence</a></p><p>{(operation as AdminOperation).manualException!.blockedReasons?.map((item: string) => <span className="badge failure" key={item}>{label(item)}</span>)}</p></div> : null}{(operation as AdminOperation).diagnostics?.length ? <div className="diagnostics"><h4>Diagnostics</h4>{(operation as AdminOperation).diagnostics!.map((item: { code: string; message: string }) => <p key={item.code}><strong>{item.code}</strong> — {item.message}</p>)}</div> : null}</> : <p>Select an order with an operation to inspect provisioning.</p>}</div><div className="detail-card action-card"><h3>Audited controls</h3><p>Every action records the current operation revision and requires a reason. Capabilities are enforced by the API.</p><label>Reason<input value={reason} maxLength={1000} onChange={(event) => setReason(event.target.value)}/></label><label>Recovery point (UTC)<input type="datetime-local" value={recoveryPoint} onChange={(event) => setRecoveryPoint(event.target.value)}/></label><label>Refund amount (minor units, optional)<input inputMode="numeric" value={amountMinor} onChange={(event) => setAmountMinor(event.target.value)}/></label><div className="action-buttons">{(["retry", "restore", "hold", "refund"] as const).map((action) => <button key={action} className="secondary-button" disabled={busy || fixtureMode || !operation || !can(`dedicated-database.actions.${action}`)} onClick={() => void submitAction(action)}>{label(action)}</button>)}</div><p className="subtle">Controls are disabled in fixture mode. Fixture examples are not provider or staging evidence.</p></div></div> : <p className="empty-state">Select an order from the queue.</p>}</div></section>}
    <section className="safety-strip"><strong>Capabilities are explicit. Entitlements are earned.</strong><p>Only server-confirmed entitlement state enables access. Recovery capabilities remain unconfirmed unless the server says otherwise.</p></section></main><footer>TECK CLOUD · DEDICATED POSTGRESQL <span>Built for reliable data services</span></footer></div>;
}
function OrderDetail({ order, operation, onClose }: { order: Order; operation: Operation | AdminOperation | null; onClose: () => void }) { return <section className="detail-panel"><div className="panel-heading"><h3>{order.orderId}</h3><button className="icon-button" aria-label="Close order details" onClick={onClose}>×</button></div><div className="detail-grid"><div><small>Status</small><strong>{label(order.status)}</strong></div><div><small>Payment</small><strong>{label(order.paymentStatus)}</strong></div><div><small>Price snapshot</small><strong>{formatMoney(order.priceSnapshot)}</strong></div><div><small>Entitlement</small><strong>{label(order.entitlementSummary.status)}</strong></div></div>{order.entitlementSummary.status !== "active" && <p className="warning-note">Entitlements are not active until authoritative server state confirms activation.</p>}{operation && <div className="progress-block"><strong>{label(operation.phase)} · {label(operation.status)}</strong><p>{operation.succeededStepCount}/{operation.requiredStepCount} succeeded · {operation.failedStepCount} failed</p>{operation.steps.map((step) => <div className="step-row" key={step.stepId}>{label(step.stepId)}<small>{label(step.status)} {step.failedCode}</small></div>)}</div>}</section>; }
const root = document.getElementById("root"); if (!root) throw new Error("Missing application root"); createRoot(root).render(<React.StrictMode><App/></React.StrictMode>);
