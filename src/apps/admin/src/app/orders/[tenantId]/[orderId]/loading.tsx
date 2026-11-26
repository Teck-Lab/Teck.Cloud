import { Notice } from "@teck/ui/blocks/notice";

export default function OrderDetailLoading() {
  return (
    <section
      aria-busy="true"
      aria-live="polite"
      className="mx-auto flex w-full max-w-5xl flex-col gap-6 px-6 py-10"
    >
      <div className="h-3 w-48 rounded-sm bg-muted" />
      <div className="h-7 w-80 max-w-full rounded-md bg-muted" />
      <Notice tone="info">Loading order…</Notice>
      <div className="grid gap-4 lg:grid-cols-3">
        <div className="h-40 rounded-lg border border-border bg-card" />
        <div className="h-40 rounded-lg border border-border bg-card" />
        <div className="h-40 rounded-lg border border-border bg-card" />
      </div>
    </section>
  );
}
