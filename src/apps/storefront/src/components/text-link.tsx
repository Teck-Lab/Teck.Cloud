import type { ReactNode } from "react";

const textLinkClass =
  "font-medium text-foreground underline decoration-border underline-offset-4 hover:decoration-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2";

const actionLinkClass =
  "inline-flex min-h-9 items-center justify-center rounded-md border border-input bg-background px-3 py-1.5 text-sm font-medium text-foreground shadow-sm transition-colors hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2";

export function TextLink({ href, children }: { href: string; children: ReactNode }) {
  return (
    <a href={href} className={textLinkClass}>
      {children}
    </a>
  );
}

export function ActionLink({ href, children }: { href: string; children: ReactNode }) {
  return (
    <a href={href} className={actionLinkClass}>
      {children}
    </a>
  );
}
