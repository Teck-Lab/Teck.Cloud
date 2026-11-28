import { describe, expect, test } from "bun:test";
import { loadCommerce } from "./load";
import {
  CommerceHttpError,
  CommerceInvalidResponseError,
  CommerceUnavailableError,
} from "./problems";

describe("commerce load results", () => {
  test("returns a value on success", async () => {
    expect(await loadCommerce(async () => "value")).toEqual({
      ok: true,
      value: "value",
    });
  });

  test("maps only a validated scoped 404 to not-found", async () => {
    const recognised = await loadCommerce(async () => {
      throw new CommerceHttpError(404, { status: 404, code: "not_found" });
    });
    expect(recognised).toEqual({ ok: false, kind: "not_found" });

    const malformed = await loadCommerce(async () => {
      throw new CommerceHttpError(404, null);
    });
    expect(malformed).toEqual({ ok: false, kind: "error" });
  });

  test("maps the three validated checkout conflicts", async () => {
    for (const code of ["stale_revision", "idempotency_conflict", "invalid_transition"] as const) {
      const result = await loadCommerce(async () => {
        throw new CommerceHttpError(409, { status: 409, code });
      });
      expect(result).toEqual({ ok: false, kind: "conflict", code });
    }
  });

  test("maps authorization, denial and dependency failures", async () => {
    for (const [status, code, kind] of [
      [401, "unauthenticated", "unauthorized"],
      [403, "forbidden", "forbidden"],
      [503, "dependency_unavailable", "unavailable"],
    ] as const) {
      const result = await loadCommerce(async () => {
        throw new CommerceHttpError(status, { status, code });
      });
      expect(result).toEqual({ ok: false, kind });
    }
  });

  test("does not expose transport failures as a trusted gateway status", async () => {
    expect(
      await loadCommerce(async () => {
        throw new CommerceUnavailableError();
      }),
    ).toEqual({ ok: false, kind: "unavailable" });
    expect(
      await loadCommerce(async () => {
        throw new CommerceInvalidResponseError();
      }),
    ).toEqual({ ok: false, kind: "error" });
  });
});
