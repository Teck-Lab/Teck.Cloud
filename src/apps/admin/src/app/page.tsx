export default function AdminHomePage() {
  return (
    <section className="mx-auto flex w-full max-w-3xl flex-col gap-6 px-6 py-12">
      <header className="flex flex-col gap-2">
        <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
          Teck.Cloud staff
        </p>
        <h1 className="text-3xl font-semibold tracking-tight text-foreground">
          Commerce administration
        </h1>
        <p className="max-w-xl text-sm text-muted-foreground">
          Review payments, staff decisions, readiness evidence and the audit trail for one tenant at
          a time.
        </p>
      </header>

      <div className="rounded-lg border border-border bg-card p-5">
        <h2 className="text-sm font-medium text-foreground">Open a tenant</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Choose a single Keycloak organization UUID to view its orders. There is no global order
          view.
        </p>
        <a
          className="mt-4 inline-flex items-center rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          href="/orders"
        >
          Select tenant
        </a>
      </div>

      <div className="grid gap-3 text-sm text-muted-foreground sm:grid-cols-2">
        <p>
          Decisions require a reason and are final. Readiness stays derived from current placement
          verification.
        </p>
        <p>Access needs an authenticated staff session with the matching commerce permissions.</p>
      </div>
    </section>
  );
}
