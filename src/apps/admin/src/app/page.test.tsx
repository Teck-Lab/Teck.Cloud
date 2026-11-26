import { afterEach, describe, expect, it } from "bun:test";
import { cleanup, render, screen } from "@testing-library/react";
import AdminHomePage from "./page";

afterEach(cleanup);

describe("Admin home", () => {
  it("points staff to a single-tenant order view without prefetching global data", () => {
    render(<AdminHomePage />);

    expect(screen.getByRole("heading", { name: "Commerce administration" })).toBeDefined();
    expect(screen.getByText(/There is no global order view/)).toBeDefined();
    expect(screen.getByRole("link", { name: "Select tenant" }).getAttribute("href")).toBe(
      "/orders",
    );
    expect(document.body.textContent ?? "").not.toContain("license");
  });
});
