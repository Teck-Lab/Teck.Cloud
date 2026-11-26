import { Notice } from "@teck/ui/blocks/notice";

export default function RootLoading() {
  return (
    <section
      aria-busy="true"
      aria-live="polite"
      className="mx-auto flex w-full max-w-3xl flex-col gap-6 px-6 py-12"
    >
      <div className="h-3 w-32 rounded-sm bg-muted" />
      <div className="h-9 w-72 max-w-full rounded-md bg-muted" />
      <Notice tone="info">Loading commerce administration…</Notice>
    </section>
  );
}
