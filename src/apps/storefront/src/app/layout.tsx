import type { Metadata } from "next";
import Link from "next/link";
import type { ReactNode } from "react";
import "./globals.css";

const navigation = [
  { href: "/catalog", label: "Catalog" },
  { href: "/basket", label: "Basket" },
  { href: "/orders", label: "Orders" },
] as const;

export const metadata: Metadata = {
  title: {
    default: "Teck Storefront",
    template: "%s · Teck Storefront",
  },
  description: "Browse Teck database tiers, manage a basket and follow order status.",
};

export default function RootLayout({ children }: Readonly<{ children: ReactNode }>) {
  return (
    <html lang="en">
      <body className="min-h-screen bg-background text-foreground antialiased">
        <a
          href="#main-content"
          className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-50 focus:rounded-md focus:bg-primary focus:px-4 focus:py-2 focus:text-primary-foreground focus:outline-none focus:ring-2 focus:ring-ring"
        >
          Skip to content
        </a>
        <header className="border-b border-border bg-card">
          <div className="mx-auto flex max-w-6xl flex-wrap items-center gap-x-6 gap-y-3 px-4 py-3 sm:px-6">
            <Link href="/" className="text-base font-semibold tracking-tight text-foreground">
              Teck Storefront
            </Link>
            <nav aria-label="Storefront" className="flex flex-wrap items-center gap-1 text-sm">
              {navigation.map((item) => (
                <Link
                  key={item.href}
                  href={item.href}
                  className="rounded-md px-3 py-1.5 text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                >
                  {item.label}
                </Link>
              ))}
            </nav>
          </div>
        </header>
        <main id="main-content" className="mx-auto max-w-6xl px-4 py-8 sm:px-6">
          {children}
        </main>
      </body>
    </html>
  );
}
