import type { BasketActionResult, BasketSaveInput } from "@/commerce/contracts";
import type { components } from "./api.generated";
import { toBasketView } from "./map";
import {
  CommerceHttpError,
  CommerceInvalidResponseError,
  CommerceUnavailableError,
} from "./problems";

type Basket = components["schemas"]["Basket"];

const ADD_ON_CODE_PATTERN = /^[A-Za-z][A-Za-z0-9_-]{0,63}$/;
const UINT32_MAX = 4294967295;

export type BasketSaveDeps = {
  putBasket: (input: BasketSaveInput) => Promise<Basket>;
};

export function validateBasketSaveInput(value: unknown): BasketSaveInput | null {
  if (typeof value !== "object" || value === null || Array.isArray(value)) {
    return null;
  }
  const input = value as {
    selection?: unknown;
    expectedRevision?: unknown;
  };

  if (
    typeof input.expectedRevision !== "number" ||
    !Number.isSafeInteger(input.expectedRevision) ||
    input.expectedRevision < 0 ||
    input.expectedRevision > UINT32_MAX
  ) {
    return null;
  }

  if (input.selection === null) {
    return { selection: null, expectedRevision: input.expectedRevision };
  }

  const selection = input.selection;
  if (typeof selection !== "object" || selection === null || Array.isArray(selection)) {
    return null;
  }
  const candidate = selection as { offerId?: unknown; addOnCodes?: unknown };
  if (
    typeof candidate.offerId !== "string" ||
    candidate.offerId.length === 0 ||
    candidate.offerId.length > 64
  ) {
    return null;
  }
  if (!Array.isArray(candidate.addOnCodes)) {
    return null;
  }
  if (candidate.addOnCodes.length > 20) {
    return null;
  }

  const seen = new Set<string>();
  const addOnCodes: string[] = [];
  for (const code of candidate.addOnCodes) {
    if (typeof code !== "string" || !ADD_ON_CODE_PATTERN.test(code)) {
      return null;
    }
    if (seen.has(code)) {
      return null;
    }
    seen.add(code);
    addOnCodes.push(code);
  }

  return {
    selection: { offerId: candidate.offerId, addOnCodes },
    expectedRevision: input.expectedRevision,
  };
}

function toBasketFailure(error: unknown): BasketActionResult {
  if (error instanceof CommerceHttpError && error.problem !== null) {
    return { status: error.problem.status, code: error.problem.code };
  }
  if (error instanceof CommerceInvalidResponseError || error instanceof CommerceHttpError) {
    return { status: 502 };
  }
  if (error instanceof CommerceUnavailableError) {
    return { status: 503, code: "dependency_unavailable" };
  }
  return { status: 503, code: "dependency_unavailable" };
}

export async function performBasketSave(
  deps: BasketSaveDeps,
  value: unknown,
): Promise<BasketActionResult> {
  const input = validateBasketSaveInput(value);
  if (input === null) {
    return { status: 400, code: "invalid_request" };
  }

  try {
    const basket = await deps.putBasket(input);
    return { status: 200, basket: toBasketView(basket) };
  } catch (error) {
    return toBasketFailure(error);
  }
}
