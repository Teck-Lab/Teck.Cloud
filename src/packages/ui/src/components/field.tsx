import type { ReactNode } from "react";
import { cn } from "../lib/utils";

export interface FieldProps {
  label: string;
  htmlFor?: string;
  children: ReactNode;
  description?: string;
  error?: string;
  className?: string;
}

export function Field({ label, htmlFor, children, description, error, className }: FieldProps) {
  const classes = cn("grid gap-1.5", className);
  const body = (
    <>
      <label className="text-sm font-medium leading-none text-foreground" htmlFor={htmlFor}>
        {label}
      </label>
      {children}
      {description ? <p className="text-xs text-muted-foreground">{description}</p> : null}
      {error ? (
        <p className="text-xs font-medium text-destructive" role="alert">
          {error}
        </p>
      ) : null}
    </>
  );
  if (htmlFor) return <div className={classes}>{body}</div>;
  return (
    <fieldset aria-label={label} className={classes}>
      {body}
    </fieldset>
  );
}
