export type CheckoutResult =
  | {
      status: 201;
      order: { id: string; status: string };
      checkoutUrl: string | null;
      checkoutDenied?: boolean;
    }
  | {
      status: number;
      code?: string;
      order?: never;
      checkoutUrl?: never;
      checkoutDenied?: never;
    };
