import { Notice } from "@teck/ui/blocks/notice";
import { StatusBadge } from "@teck/ui/blocks/status-badge";
import { Badge } from "@teck/ui/components/badge";
import type { ReactNode } from "react";
import {
  formatMoney,
  formatTimestamp,
  MESSAGES,
  type OrderLoadResult,
} from "@/server/commerce/types";
import type { MutationAction } from "./action-state";
import { DecisionForm } from "./decision-form";
import { EvidenceForm } from "./evidence-form";

export type DetailActions = {
  decide: MutationAction;
  recordEvidence: MutationAction;
};

type OrderDetailViewProps = {
  result: OrderLoadResult;
  actions: DetailActions;
};

const PAYMENT_LABELS: Record<string, string> = {
  pending: "Pending",
  paid: "Paid",
};

const APPROVAL_LABELS: Record<string, string> = {
  pending: "Pending",
  approved: "Approved",
  rejected: "Rejected",
};

const FULFILLMENT_LABELS: Record<string, string> = {
  awaitingEvidence: "Awaiting evidence",
  verifying: "Verifying",
  ready: "Ready",
  failed: "Failed",
};

const FAILURE_LABELS: Record<string, string> = {
  binding_mismatch: "Binding mismatch",
  capability_unavailable: "Required capability unavailable",
  probe_unavailable: "Probe unavailable; retrying",
};

const AUDIT_LABELS: Record<string, string> = {
  approve: "Approved",
  reject: "Rejected",
  recordPlacementEvidence: "Placement evidence recorded",
};

function StateNotice({ children }: { children: ReactNode }) {
  return <Notice tone="error">{children}</Notice>;
}

function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="mt-1 break-words text-sm text-foreground">{children}</dd>
    </div>
  );
}

export function OrderDetailView({ result, actions }: OrderDetailViewProps) {
  if (result.kind !== "ok") {
    return (
      <section className="mx-auto flex w-full max-w-5xl flex-col gap-6 px-6 py-10">
        <header className="flex flex-col gap-1">
          <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            Commerce administration
          </p>
          <h1 className="text-2xl font-semibold tracking-tight text-foreground">Order</h1>
        </header>
        {result.kind === "invalid" ? <StateNotice>{result.message}</StateNotice> : null}
        {result.kind === "unauthorized" ? (
          <div className="flex flex-col gap-3">
            <StateNotice>{MESSAGES.unauthorized}</StateNotice>
            <a className="text-sm underline" href="/sign-in">
              Sign in
            </a>
          </div>
        ) : null}
        {result.kind === "forbidden" ? <StateNotice>{MESSAGES.forbidden}</StateNotice> : null}
        {result.kind === "notFound" ? <StateNotice>{MESSAGES.notFound}</StateNotice> : null}
        {result.kind === "unavailable" ? <StateNotice>{MESSAGES.unavailable}</StateNotice> : null}
        {result.kind === "error" ? <StateNotice>{result.message}</StateNotice> : null}
        <a className="text-sm underline" href="/orders">
          Back to orders
        </a>
      </section>
    );
  }

  const { order, tenantId, orderId } = result;
  const canDecide = order.status === "pendingApproval" && order.approval.status === "pending";
  const canRecordEvidence = order.approval.status === "approved" && order.payment.status === "paid";
  const tenantHref = `/orders?tenantId=${encodeURIComponent(tenantId)}`;

  return (
    <section className="mx-auto flex w-full max-w-5xl flex-col gap-6 px-6 py-10">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <a className="text-sm text-muted-foreground underline" href={tenantHref}>
          Back to tenant orders
        </a>
        <a className="text-sm text-muted-foreground underline" href="/orders">
          Change tenant
        </a>
      </div>

      <header className="flex flex-col gap-3">
        <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
          Commerce administration
        </p>
        <h1 className="break-all font-mono text-xl font-semibold tracking-tight text-foreground">
          {orderId}
        </h1>
        <div className="flex flex-wrap items-center gap-2">
          <StatusBadge status={order.status} />
          <Badge>{order.tier === "dedicated" ? "Dedicated" : "Shared"}</Badge>
          <span className="text-sm font-medium text-foreground">{formatMoney(order.total)}</span>
        </div>
        <p className="text-xs text-muted-foreground">
          Revision {order.revision} · Updated {formatTimestamp(order.updatedAt)} · Created{" "}
          {formatTimestamp(order.createdAt)}
        </p>
      </header>

      <div className="grid gap-4 lg:grid-cols-3">
        <article className="rounded-lg border border-border bg-card p-4">
          <h2 className="text-sm font-medium text-foreground">Payment</h2>
          <dl className="mt-3 flex flex-col gap-3">
            <Fact label="Status">{PAYMENT_LABELS[order.payment.status] ?? "Unknown"}</Fact>
            <Fact label="Paid through">{formatTimestamp(order.payment.paidThrough)}</Fact>
          </dl>
        </article>

        <article className="rounded-lg border border-border bg-card p-4">
          <h2 className="text-sm font-medium text-foreground">Decision</h2>
          <dl className="mt-3 flex flex-col gap-3">
            <Fact label="Status">{APPROVAL_LABELS[order.approval.status] ?? "Unknown"}</Fact>
            <Fact label="Staff actor">{order.approval.actorId ?? "Not recorded"}</Fact>
            <Fact label="Reason">{order.approval.reason ?? "Not recorded"}</Fact>
            <Fact label="Recorded">{formatTimestamp(order.approval.occurredAt)}</Fact>
          </dl>
        </article>

        <article className="rounded-lg border border-border bg-card p-4">
          <h2 className="text-sm font-medium text-foreground">Readiness</h2>
          <dl className="mt-3 flex flex-col gap-3">
            <Fact label="Status">{FULFILLMENT_LABELS[order.fulfillment.status] ?? "Unknown"}</Fact>
            <Fact label="Failure">
              {order.fulfillment.failureCode === null
                ? "None recorded"
                : (FAILURE_LABELS[order.fulfillment.failureCode] ?? order.fulfillment.failureCode)}
            </Fact>
            <Fact label="Placement">
              <span className="font-mono text-xs">
                {order.fulfillment.placementId ?? "Not recorded"}
              </span>
            </Fact>
            <Fact label="Evidence reference">
              <span className="break-all font-mono text-xs">
                {order.fulfillment.evidenceReference ?? "Not recorded"}
              </span>
            </Fact>
            <Fact label="Observed">{formatTimestamp(order.fulfillment.observedAt)}</Fact>
          </dl>
        </article>
      </div>

      {canDecide ? (
        <article className="flex flex-col gap-3 rounded-lg border border-border bg-card p-4">
          <h2 className="text-sm font-medium text-foreground">Record decision</h2>
          <p className="text-xs text-muted-foreground">
            Approve or reject this paid order using its current revision {order.revision}.
          </p>
          <DecisionForm
            tenantId={tenantId}
            orderId={orderId}
            revision={order.revision}
            action={actions.decide}
          />
        </article>
      ) : order.approval.status !== "pending" ? (
        <Notice tone="info">A final staff decision is recorded. This page cannot change it.</Notice>
      ) : order.status === "pendingPayment" ? (
        <Notice tone="info">A staff decision becomes available after payment is confirmed.</Notice>
      ) : null}

      {canRecordEvidence ? (
        <article className="flex flex-col gap-3 rounded-lg border border-border bg-card p-4">
          <h2 className="text-sm font-medium text-foreground">Manual placement evidence</h2>
          <p className="text-xs text-muted-foreground">
            Record one bounded audit reference for this tenant and tier. The current revision is{" "}
            {order.revision}.
          </p>
          <EvidenceForm
            tenantId={tenantId}
            orderId={orderId}
            revision={order.revision}
            action={actions.recordEvidence}
          />
        </article>
      ) : null}

      <article className="rounded-lg border border-border bg-card p-4">
        <h2 className="text-sm font-medium text-foreground">Audit</h2>
        {order.audit.length === 0 ? (
          <p className="mt-2 text-sm text-muted-foreground">No audit entries recorded.</p>
        ) : (
          <ol className="mt-3 flex flex-col divide-y divide-border">
            {order.audit.map((entry) => (
              <li
                key={`${entry.occurredAt}-${entry.action}-${entry.actorId}`}
                className="flex flex-col gap-1 py-3 first:pt-0 last:pb-0"
              >
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="text-sm font-medium text-foreground">
                    {AUDIT_LABELS[entry.action] ?? entry.action}
                  </span>
                  <time className="text-xs text-muted-foreground" dateTime={entry.occurredAt}>
                    {formatTimestamp(entry.occurredAt)}
                  </time>
                </div>
                <p className="text-xs text-muted-foreground">Actor {entry.actorId}</p>
                {entry.reason === null ? null : (
                  <p className="break-words text-sm text-foreground">{entry.reason}</p>
                )}
              </li>
            ))}
          </ol>
        )}
      </article>
    </section>
  );
}
