import { Notice } from "@teck/ui/blocks/notice";
import { StatusBadge } from "@teck/ui/blocks/status-badge";
import type { ReactNode } from "react";
import type { OrderDetailView } from "@/commerce/contracts";
import {
  addOnsLabel,
  canDownloadLicense,
  formatMoney,
  formatTimestamp,
  tierLabel,
} from "@/commerce/contracts";
import { ActionLink } from "@/components/text-link";

function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="mt-1 text-sm text-foreground">{children}</dd>
    </div>
  );
}

export function OrderFacts({
  order,
  checkoutNotice,
}: {
  order: OrderDetailView;
  checkoutNotice?: string | null;
}) {
  const licenseAvailable = canDownloadLicense(order);

  return (
    <section
      aria-labelledby="order-summary-heading"
      className="rounded-lg border border-border bg-card p-4 shadow-sm sm:p-5"
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="min-w-0">
          <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Order</p>
          <h2
            id="order-summary-heading"
            className="mt-1 break-all text-lg font-semibold text-card-foreground"
          >
            {order.id}
          </h2>
        </div>
        <StatusBadge status={order.status} />
      </div>

      {checkoutNotice === undefined || checkoutNotice === null ? null : (
        <div className="mt-3">
          <Notice tone="info">{checkoutNotice}</Notice>
        </div>
      )}

      <dl className="mt-4 grid gap-4 sm:grid-cols-3">
        <Fact label="Plan">{tierLabel(order.tier)}</Fact>
        <Fact label="Add-ons">{addOnsLabel(order.addOnCodes)}</Fact>
        <Fact label="Total">{formatMoney(order.total)}</Fact>
        <Fact label="Order revision">{order.revision}</Fact>
        <Fact label="Created">
          <time dateTime={order.createdAt}>{formatTimestamp(order.createdAt)}</time>
        </Fact>
        <Fact label="Updated">
          <time dateTime={order.updatedAt}>{formatTimestamp(order.updatedAt)}</time>
        </Fact>
      </dl>

      <div className="mt-5 border-t border-border pt-4">
        <h3 className="text-sm font-semibold text-foreground">License</h3>
        {licenseAvailable && order.license !== null ? (
          <div className="mt-2 space-y-3">
            <dl className="grid gap-4 sm:grid-cols-3">
              <Fact label="Signing key">{order.license.keyId}</Fact>
              <Fact label="License revision">{order.license.revision}</Fact>
              <Fact label="Expires">
                <time dateTime={order.license.expiresAt}>
                  {formatTimestamp(order.license.expiresAt)}
                </time>
              </Fact>
            </dl>
            <ActionLink href={`/orders/${encodeURIComponent(order.id)}/license`}>
              Download license
            </ActionLink>
          </div>
        ) : (
          <p className="mt-2 text-sm text-muted-foreground">
            A license download appears after this order becomes active.
          </p>
        )}
      </div>
    </section>
  );
}
