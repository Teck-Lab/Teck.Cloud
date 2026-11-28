import { Notice } from "@teck/ui/blocks/notice";

export default function OrdersLoading() {
  return (
    <section
      aria-busy="true"
      aria-live="polite"
      className="mx-auto flex w-full max-w-5xl flex-col gap-6 px-6 py-10"
    >
      <div className="flex flex-col gap-2">
        <div className="h-3 w-40 rounded-sm bg-muted" />
        <div className="h-7 w-48 rounded-md bg-muted" />
      </div>
      <Notice tone="info">Loading orders…</Notice>
      <div className="h-40 rounded-lg border border-border bg-card" />
    </section>
  );
}
