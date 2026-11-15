import type { ClassKey } from "keycloakify/login";

// Keep native Keycloakify security protocols and apply the same shadcn palette.
export const securityClasses = {
  kcBodyClass: "bg-background text-foreground",
  kcLoginClass: "min-h-screen flex flex-col items-center justify-center p-4",
  kcHeaderClass: "mb-4 text-xl font-semibold",
  kcFormCardClass: "w-full max-w-md rounded-lg border bg-card text-card-foreground p-6 shadow-sm",
  kcFormHeaderClass: "mb-6 text-center text-xl font-semibold",
  kcFormGroupClass: "mb-4 space-y-2",
  kcLabelClass: "text-sm font-medium",
  kcInputClass: "flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm",
  kcButtonClass: "inline-flex items-center justify-center rounded-md px-4 py-2 text-sm font-medium",
  kcButtonPrimaryClass: "bg-primary text-primary-foreground hover:opacity-90",
  kcButtonDefaultClass: "border border-input bg-background",
  kcButtonBlockClass: "w-full",
  kcInputErrorMessageClass: "text-sm text-destructive",
  kcAlertClass: "mb-4 rounded-md border p-3 text-sm",
  kcRecoveryCodesList: "grid grid-cols-2 gap-2 font-mono text-sm",
} satisfies Partial<Record<ClassKey, string>>;
