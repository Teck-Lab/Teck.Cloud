import {
  CommerceHttpError,
  CommerceInputError,
  CommerceInvalidResponseError,
  type CommerceProblemCode,
  CommerceUnavailableError,
} from "./problems";

export type CommerceFailureKind =
  | "unauthorized"
  | "forbidden"
  | "not_found"
  | "conflict"
  | "unavailable"
  | "error";

export type CommerceLoadResult<T> =
  | { ok: true; value: T }
  | { ok: false; kind: CommerceFailureKind; code?: CommerceProblemCode };

export async function loadCommerce<T>(operation: () => Promise<T>): Promise<CommerceLoadResult<T>> {
  try {
    return { ok: true, value: await operation() };
  } catch (error) {
    if (error instanceof CommerceHttpError) {
      if (error.problem === null) {
        return { ok: false, kind: "error" };
      }
      switch (error.problem.status) {
        case 401:
          return { ok: false, kind: "unauthorized" };
        case 403:
          return { ok: false, kind: "forbidden" };
        case 404:
          return { ok: false, kind: "not_found" };
        case 409:
          return { ok: false, kind: "conflict", code: error.problem.code };
        case 503:
          return { ok: false, kind: "unavailable" };
        default:
          return { ok: false, kind: "error" };
      }
    }
    if (error instanceof CommerceUnavailableError) {
      return { ok: false, kind: "unavailable" };
    }
    if (error instanceof CommerceInvalidResponseError || error instanceof CommerceInputError) {
      return { ok: false, kind: "error" };
    }
    return { ok: false, kind: "error" };
  }
}
