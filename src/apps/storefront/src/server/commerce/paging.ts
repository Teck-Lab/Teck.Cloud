export type CursorRead = { kind: "none" } | { kind: "cursor"; value: string } | { kind: "invalid" };

/**
 * The only accepted cursor is the opaque value in the current app URL. Repeated
 * query parameters, empty values and over-long values are rejected rather than
 * trimmed or converted into another page.
 */
export function readCursor(value: string | string[] | undefined): CursorRead {
  if (value === undefined) {
    return { kind: "none" };
  }
  if (typeof value !== "string" || value.length === 0 || value.length > 512) {
    return { kind: "invalid" };
  }
  return { kind: "cursor", value };
}
