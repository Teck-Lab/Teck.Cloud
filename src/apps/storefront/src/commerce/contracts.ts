/**
 * Serializable view contracts shared by Storefront server pages/actions and
 * client components. Gateway DTOs are narrowed into these shapes on the server
 * so browser props never carry staff fields, licenses or bearer material.
 */

export type MoneyView = {
  amountMinor: number;
  currency: string;
};

export type AddOnView = {
  code: string;
  displayName: string;
  price: MoneyView;
};

export type OfferView = {
  offerId: string;
  tier: string;
  displayName: string;
  basePrice: MoneyView;
  addOns: AddOnView[];
};

export type BasketSelectionView = {
  offerId: string;
  addOnCodes: string[];
};

export type BasketView = {
  selection: BasketSelectionView | null;
  revision: number;
  total: MoneyView | null;
};

export type BasketSaveInput = {
  selection: BasketSelectionView | null;
  expectedRevision: number;
};

export type BasketActionResult =
  | { status: 200; basket: BasketView }
  | { status: number; code?: string };

export type OrderSummaryView = {
  id: string;
  tier: string;
  addOnCodes: string[];
  total: MoneyView;
  status: string;
  revision: number;
  createdAt: string;
  updatedAt: string;
};

export type LicenseView = {
  keyId: string;
  revision: number;
  expiresAt: string;
};

export type OrderDetailView = OrderSummaryView & {
  subscriptionId: string;
  license: LicenseView | null;
};

export type OrderTableRow = {
  id: string;
  href: string;
  tier: string;
  status: string;
  total: string;
};

/**
 * ISO 4217 minor units do not always use hundredths: JPY has no fraction and
 * KWD has three decimals. Resolve the exponent through Intl, then split the
 * amount with BigInt so the minor-to-major ratio stays exact up to the
 * contract's safe-integer bound instead of accumulating binary float error.
 */
function formatMinorUnits(amountMinor: number, currency: string): string {
  const formatter = new Intl.NumberFormat("en-US", {
    style: "currency",
    currency,
  });
  const fractionDigits = formatter.resolvedOptions().maximumFractionDigits ?? 2;
  const scale = 10n ** BigInt(fractionDigits);
  const minor = BigInt(amountMinor);
  const fraction = (minor < 0n ? -minor : minor) % scale;

  return formatter
    .formatToParts(Number(minor / scale))
    .map((part) =>
      part.type === "fraction"
        ? { ...part, value: fraction.toString().padStart(fractionDigits, "0") }
        : part,
    )
    .map((part) => part.value)
    .join("");
}

export function formatMoney(money: MoneyView | null): string {
  if (money === null) {
    return "Not set";
  }

  try {
    return formatMinorUnits(money.amountMinor, money.currency);
  } catch {
    return `${money.amountMinor} ${money.currency}`;
  }
}

export function tierLabel(tier: string): string {
  switch (tier) {
    case "shared":
      return "Shared";
    case "dedicated":
      return "Dedicated";
    default:
      return "Other";
  }
}

export function addOnsLabel(codes: readonly string[]): string {
  return codes.length === 0 ? "None" : codes.join(", ");
}

export function formatTimestamp(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  return new Intl.DateTimeFormat("en-US", {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
    timeZone: "UTC",
    timeZoneName: "short",
  }).format(date);
}

export function canDownloadLicense(order: {
  status: string;
  license: LicenseView | null;
}): boolean {
  return order.status === "active" && order.license !== null;
}

export function orderHref(orderId: string): string {
  return `/orders/${encodeURIComponent(orderId)}`;
}
