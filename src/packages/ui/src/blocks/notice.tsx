import type { ReactNode } from "react";
import { cn } from "../lib/utils";

export interface NoticeProps {
  children: ReactNode;
  tone?: "info" | "error";
  className?: string;
}

export function Notice({ children, tone = "info", className }: NoticeProps) {
  const isError = tone === "error";
  return (
    <div
      className={cn(
        "rounded-md border px-3 py-2 text-sm",
        isError
          ? "border-destructive/50 bg-destructive/10 text-destructive"
          : "border-border bg-muted text-foreground",
        className,
      )}
      role={isError ? "alert" : "status"}
    >
      {children}
    </div>
  );
}
