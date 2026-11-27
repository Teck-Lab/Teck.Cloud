import type { ReactNode } from "react";
import { cn } from "../lib/utils";

export interface NoticeProps {
  children: ReactNode;
  tone?: "info" | "error";
  className?: string;
}

export function Notice({ children, tone = "info", className }: NoticeProps) {
  const isError = tone === "error";
  // Destructive text on its 10% tint measures ~3.3:1 light / ~2.0:1 dark, so the
  // error tone keeps the readable foreground token (~15.7:1 / ~20.4:1).
  return (
    <div
      className={cn(
        "rounded-md border px-3 py-2 text-sm",
        isError
          ? "border-destructive/50 bg-destructive/10 text-foreground"
          : "border-border bg-muted text-foreground",
        className,
      )}
      role={isError ? "alert" : "status"}
    >
      {children}
    </div>
  );
}
