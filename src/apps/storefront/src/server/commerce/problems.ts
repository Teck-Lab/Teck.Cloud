/**
 * Customer commerce problem handling.
 *
 * Only a shape-valid Problem for the actual HTTP status, with the code that
 * status is allowed to carry, is treated as a recognised gateway response.
 * Everything else remains an opaque failure so a forged or malformed body can
 * never turn into a scoped not-found, conflict or redirect decision.
 */

import { isRecord } from "./guards";

export const COMMERCE_PROBLEM_CODES = [
  "invalid_request",
  "unauthenticated",
  "forbidden",
  "not_found",
  "idempotency_conflict",
  "stale_revision",
  "invalid_transition",
  "unsupported_option",
  "dependency_unavailable",
] as const;

export type CommerceProblemCode = (typeof COMMERCE_PROBLEM_CODES)[number];

export type CommerceProblem = {
  status: number;
  code: CommerceProblemCode;
};

const STATUS_CODES: Readonly<Record<number, readonly CommerceProblemCode[]>> = {
  400: ["invalid_request"],
  401: ["unauthenticated"],
  403: ["forbidden"],
  404: ["not_found"],
  409: ["idempotency_conflict", "stale_revision", "invalid_transition"],
  422: ["unsupported_option"],
  503: ["dependency_unavailable"],
};

export function parseCommerceProblem(value: unknown, httpStatus: number): CommerceProblem | null {
  if (!isRecord(value)) {
    return null;
  }

  const allowed = STATUS_CODES[httpStatus];
  if (allowed === undefined) {
    return null;
  }

  if (typeof value.type !== "string" || value.type.length === 0) {
    return null;
  }
  if (typeof value.title !== "string" || value.title.length === 0) {
    return null;
  }
  if (value.status !== httpStatus) {
    return null;
  }
  if (typeof value.code !== "string" || !allowed.includes(value.code as CommerceProblemCode)) {
    return null;
  }

  return { status: httpStatus, code: value.code as CommerceProblemCode };
}

export class CommerceHttpError extends Error {
  readonly status: number;
  readonly problem: CommerceProblem | null;

  constructor(status: number, problem: CommerceProblem | null) {
    super(
      problem === null
        ? `Commerce gateway returned HTTP ${status}`
        : `Commerce gateway returned ${problem.code}`,
    );
    this.name = "CommerceHttpError";
    this.status = status;
    this.problem = problem;
  }
}

export class CommerceUnavailableError extends Error {
  constructor() {
    super("Commerce gateway is temporarily unavailable");
    this.name = "CommerceUnavailableError";
  }
}

export class CommerceInvalidResponseError extends Error {
  constructor() {
    super("Commerce gateway returned an invalid response");
    this.name = "CommerceInvalidResponseError";
  }
}

export class CommerceInputError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "CommerceInputError";
  }
}
