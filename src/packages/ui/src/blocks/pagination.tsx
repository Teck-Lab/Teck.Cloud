"use client";

import { useEffect, useState } from "react";
import { cn } from "../lib/utils";

const storagePrefix = "teck-ui:pagination:v1";
const linkClassName =
  "rounded-sm font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background";

export interface PaginationProps {
  baseHref: string;
  scope: string;
  cursor: string | null;
  nextCursor: string | null;
  className?: string;
}

/** The visible tenant URL without the cursor, so cursor history stays per tenant. */
function tenantLocation(): string {
  if (typeof window === "undefined") return "";
  const params = new URLSearchParams(window.location.search);
  params.delete("cursor");
  const sorted = [...params.entries()].sort(([left], [right]) => left.localeCompare(right));
  const search = new URLSearchParams(sorted).toString();
  return `${window.location.pathname}${search ? `?${search}` : ""}`;
}

function stripCursor(href: string): string {
  const hashIndex = href.indexOf("#");
  const hash = hashIndex === -1 ? "" : href.slice(hashIndex);
  const beforeHash = hashIndex === -1 ? href : href.slice(0, hashIndex);
  const queryIndex = beforeHash.indexOf("?");
  if (queryIndex === -1) return href;
  const path = beforeHash.slice(0, queryIndex);
  const params = new URLSearchParams(beforeHash.slice(queryIndex + 1));
  params.delete("cursor");
  const search = params.toString();
  return `${path}${search ? `?${search}` : ""}${hash}`;
}

function cursorHref(baseHref: string, cursor: string): string {
  const base = stripCursor(baseHref);
  const hashIndex = base.indexOf("#");
  const hash = hashIndex === -1 ? "" : base.slice(hashIndex);
  const beforeHash = hashIndex === -1 ? base : base.slice(0, hashIndex);
  const separator = beforeHash.includes("?") ? "&" : "?";
  return `${beforeHash}${separator}cursor=${encodeURIComponent(cursor)}${hash}`;
}

function readVisits(key: string): string[] {
  if (typeof window === "undefined") return [];
  try {
    const stored = window.sessionStorage.getItem(key);
    if (!stored) return [];
    const parsed: unknown = JSON.parse(stored);
    if (!Array.isArray(parsed)) return [];
    return parsed.filter((value): value is string => typeof value === "string" && value.length > 0);
  } catch {
    return [];
  }
}

function writeVisits(key: string, visits: readonly string[]): void {
  if (typeof window === "undefined") return;
  try {
    window.sessionStorage.setItem(key, JSON.stringify(visits));
  } catch {
    // Storage can be unavailable in restricted browser contexts; visiting still works.
  }
}

function recordVisit(visits: readonly string[], cursor: string | null): string[] {
  if (cursor === null) return [];
  const index = visits.indexOf(cursor);
  if (index === -1) return [...visits, cursor];
  return visits.slice(0, index + 1);
}

export function Pagination({ baseHref, scope, cursor, nextCursor, className }: PaginationProps) {
  const [visits, setVisits] = useState<readonly string[]>([]);

  useEffect(() => {
    const key = JSON.stringify([storagePrefix, scope, baseHref, tenantLocation()]);
    const next = recordVisit(readVisits(key), cursor);
    writeVisits(key, next);
    setVisits(next);
  }, [scope, baseHref, cursor]);

  const firstHref = stripCursor(baseHref);
  const index = cursor === null ? -1 : visits.indexOf(cursor);
  const previousCursor = index > 0 ? visits[index - 1] : undefined;
  const previousHref =
    cursor === null
      ? null
      : previousCursor === undefined
        ? firstHref
        : cursorHref(baseHref, previousCursor);
  const nextHref = nextCursor === null ? null : cursorHref(baseHref, nextCursor);

  return (
    <nav
      aria-label="Pagination"
      className={cn("flex flex-wrap items-center justify-between gap-3 text-sm", className)}
    >
      <a
        aria-current={cursor === null ? "page" : undefined}
        className={linkClassName}
        href={firstHref}
      >
        First page
      </a>
      <div className="flex items-center gap-3">
        {previousHref === null ? null : (
          <a className={linkClassName} href={previousHref} rel="prev">
            Previous
          </a>
        )}
        {nextHref === null ? null : (
          <a className={linkClassName} href={nextHref} rel="next">
            Next
          </a>
        )}
      </div>
    </nav>
  );
}
