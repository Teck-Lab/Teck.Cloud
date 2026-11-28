import { Badge, type BadgeProps } from "../components/badge";

interface StatusDescriptor {
  label: string;
  variant: NonNullable<BadgeProps["variant"]>;
}

const statuses: Readonly<Record<string, StatusDescriptor>> = {
  pendingPayment: { label: "Payment pending", variant: "secondary" },
  pendingApproval: { label: "Approval pending", variant: "secondary" },
  pendingReadiness: { label: "Readiness pending", variant: "secondary" },
  active: { label: "Active", variant: "default" },
  rejected: { label: "Rejected", variant: "destructive" },
  failed: { label: "Failed", variant: "destructive" },
};

export interface StatusBadgeProps {
  status: string;
  className?: string;
}

export function StatusBadge({ status, className }: StatusBadgeProps) {
  const known = Object.hasOwn(statuses, status) ? statuses[status] : undefined;
  const descriptor: StatusDescriptor = known ?? { label: "Unknown", variant: "outline" };
  return (
    <Badge className={className} variant={descriptor.variant}>
      {descriptor.label}
    </Badge>
  );
}
