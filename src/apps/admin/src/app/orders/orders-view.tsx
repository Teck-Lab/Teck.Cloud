import { Notice } from "@teck/ui/blocks/notice";
import { OrderTable } from "@teck/ui/blocks/order-table";
import { Pagination } from "@teck/ui/blocks/pagination";
import { Button } from "@teck/ui/components/button";
import { Field } from "@teck/ui/components/field";
import { Input } from "@teck/ui/components/input";
import type { ReactNode } from "react";
import { MESSAGES, type OrdersLoadResult, toOrderRows } from "@/server/commerce/types";

type OrdersViewProps = {
  result: OrdersLoadResult;
  tenantInput?: string | null;
};

function TenantForm({ defaultValue, invalid }: { defaultValue: string; invalid?: boolean }) {
  return (
    <form
      action="/orders"
      method="get"
      className="flex flex-col gap-3 rounded-lg border border-border bg-card p-4 sm:flex-row sm:items-end"
    >
      <div className="min-w-0 flex-1">
        <Field label="Tenant UUID" htmlFor="tenant-id">
          <Input
            id="tenant-id"
            name="tenantId"
            defaultValue={defaultValue}
            required
            autoComplete="off"
            spellCheck={false}
            placeholder="00000000-0000-0000-0000-000000000000"
            aria-invalid={invalid === true}
            aria-describedby="tenant-id-help"
          />
        </Field>
        <p id="tenant-id-help" className="mt-1 text-xs text-muted-foreground">
          Enter one Keycloak organization UUID. There is no global order view.
        </p>
      </div>
      <div className="flex items-center gap-2">
        <Button type="submit">Open tenant</Button>
        {defaultValue.length > 0 ? (
          <a className="text-sm text-muted-foreground underline" href="/orders">
            Clear
          </a>
        ) : null}
      </div>
    </form>
  );
}

function StateNotice({ children }: { children: ReactNode }) {
  return <Notice tone="error">{children}</Notice>;
}

export function OrdersView({ result, tenantInput }: OrdersViewProps) {
  const defaultValue = tenantInput ?? (result.kind === "ok" ? result.tenantId : "");

  return (
    <section className="mx-auto flex w-full max-w-5xl flex-col gap-6 px-6 py-10">
      <header className="flex flex-col gap-1">
        <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
          Commerce administration
        </p>
        <h1 className="text-2xl font-semibold tracking-tight text-foreground">Orders</h1>
        <p className="max-w-2xl text-sm text-muted-foreground">
          Select one tenant, then review its latest orders. Pages are bounded and read newest first.
        </p>
      </header>

      <TenantForm
        key={defaultValue}
        defaultValue={defaultValue}
        invalid={result.kind === "invalidTenant"}
      />

      {result.kind === "selectTenant" ? <Notice tone="info">{MESSAGES.selectTenant}</Notice> : null}

      {result.kind === "invalidTenant" ? <StateNotice>{result.message}</StateNotice> : null}

      {result.kind === "unauthorized" ? (
        <div className="flex flex-col gap-3">
          <StateNotice>{MESSAGES.unauthorized}</StateNotice>
          <a className="text-sm underline" href="/sign-in">
            Sign in
          </a>
        </div>
      ) : null}

      {result.kind === "forbidden" ? <StateNotice>{MESSAGES.forbidden}</StateNotice> : null}

      {result.kind === "notFound" ? <StateNotice>{result.message}</StateNotice> : null}

      {result.kind === "unavailable" ? <StateNotice>{MESSAGES.unavailable}</StateNotice> : null}

      {result.kind === "error" ? <StateNotice>{result.message}</StateNotice> : null}

      {result.kind === "cursorRejected" ? (
        <div className="flex flex-col gap-3">
          <StateNotice>{result.message}</StateNotice>
          <a className="text-sm underline" href={result.firstPageHref}>
            First page
          </a>
        </div>
      ) : null}

      {result.kind === "ok" ? (
        <div className="flex flex-col gap-4">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h2 className="text-base font-medium text-foreground">Tenant {result.tenantId}</h2>
            <a className="text-sm text-muted-foreground underline" href={result.baseHref}>
              First page
            </a>
          </div>
          {result.items.length === 0 ? (
            <Notice tone="info">No orders are recorded for this tenant yet.</Notice>
          ) : (
            <OrderTable rows={toOrderRows(result.items, result.tenantId)} />
          )}
          {result.cursor !== null || result.nextCursor !== null ? (
            <Pagination
              baseHref={result.baseHref}
              scope={result.scope}
              cursor={result.cursor}
              nextCursor={result.nextCursor}
            />
          ) : null}
        </div>
      ) : null}
    </section>
  );
}
