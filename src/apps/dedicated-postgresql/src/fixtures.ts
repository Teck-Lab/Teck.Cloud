import databaseOffering from "../../../../docs/contracts/dedicated-postgresql/v1/example-offerings-dedicated-database.json";
import clusterOffering from "../../../../docs/contracts/dedicated-postgresql/v1/example-offerings-dedicated-cluster.json";
import paymentPending from "../../../../docs/contracts/dedicated-postgresql/v1/example-payment-pending.json";
import migrationProgress from "../../../../docs/contracts/dedicated-postgresql/v1/example-migration-progress.json";
import operationComplete from "../../../../docs/contracts/dedicated-postgresql/v1/example-operation-complete.json";
import manualException from "../../../../docs/contracts/dedicated-postgresql/v1/example-manual-exception.json";
import type { AdminOrder, AdminOperation, Offering, Operation, Option, Order } from "./api";

export const offerings: Offering[] = [...databaseOffering.items, ...clusterOffering.items].map((item) => ({
  offeringId: item.offeringId,
  displayName: item.displayName,
  kind: item.kind,
  optionIds: item.optionIds,
  includedCapabilities: item.includedCapabilities,
  selectableEntitlements: item.selectableEntitlements,
}));
export const options: Option[] = [
  { optionId: "opt_database_small", offeringId: "off_dedicated_database", displayName: "Small · 2 vCPU · 8 GiB", region: "eu-north", storageGiB: 100, price: { amountMinor: 4900, currency: "EUR", interval: "month" } },
  { optionId: "opt_cluster_small", offeringId: "off_dedicated_cluster", displayName: "Small cluster · 3 nodes", region: "eu-north", storageGiB: 300, price: { amountMinor: 14900, currency: "EUR", interval: "month" } },
];

const pendingOrder = paymentPending.order as Order;
const migration = migrationProgress.snapshot as Operation;
const complete = operationComplete.snapshot as Operation;
const exception = manualException.snapshot as AdminOperation;
export const customerOrders: Order[] = [
  pendingOrder,
  {
    ...pendingOrder,
    orderId: "ord_demo_02",
    status: "provisioning",
    paymentStatus: "paid",
    operationId: migration.operationId,
    createdAt: migration.updatedAt,
    priceSnapshot: { ...pendingOrder.priceSnapshot, amountMinor: 14900 },
  },
  {
    ...pendingOrder,
    orderId: "ord_demo_03",
    status: "complete",
    paymentStatus: "paid",
    operationId: complete.operationId,
    createdAt: complete.updatedAt,
    priceSnapshot: { ...pendingOrder.priceSnapshot, amountMinor: 14900 },
    entitlementSummary: { status: "active", validFrom: complete.updatedAt, validUntil: null },
  },
  {
    ...pendingOrder,
    orderId: "ord_demo_04",
    status: "needsAttention",
    paymentStatus: "paid",
    operationId: exception.operationId,
    createdAt: exception.updatedAt,
  },
];
export const adminOrders = customerOrders as AdminOrder[];
export const operations = [migration, complete, exception];
export const diagnostics = exception.diagnostics ?? [];