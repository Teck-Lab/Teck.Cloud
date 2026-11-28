import { TextLink } from "@/components/text-link";

export default function NotFound() {
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold tracking-tight text-foreground">Page not found</h1>
      <p className="mt-2 text-sm text-muted-foreground">
        This page is not available in the Teck Commerce console.
      </p>
      <div className="mt-4 flex flex-wrap gap-4">
        <TextLink href="/catalog">Catalog</TextLink>
        <TextLink href="/orders">Orders</TextLink>
      </div>
    </div>
  );
}
