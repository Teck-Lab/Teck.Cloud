import { afterEach, beforeEach, expect, test } from "bun:test";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { OfflineChatFixture, type OfflineChatScenario } from "./OfflineChatFixture";

// No provider, API route or key is involved. Any fetch attempt fails the run.
const originalFetch = globalThis.fetch;
let fetchCallCount = 0;

beforeEach(() => {
  fetchCallCount = 0;
  globalThis.fetch = (() => {
    fetchCallCount += 1;
    throw new Error("The offline chat fixture must not call fetch.");
  }) as unknown as typeof fetch;
});

afterEach(() => {
  cleanup();
  globalThis.fetch = originalFetch;
});

function startFixture(scenario: OfflineChatScenario) {
  render(<OfflineChatFixture scenario={scenario} />);
  fireEvent.click(screen.getByRole("button", { name: "Send fixture message" }));
}

test("streams two scripted text parts with controlled partial timing offline", async () => {
  startFixture("timed");

  await screen.findByText("Partial fixture response");
  expect(screen.queryByText(/completed after the pause/)).toBeNull();

  await screen.findByText(/completed after the pause/, undefined, { timeout: 3000 });
  await waitFor(() => {
    expect(screen.getByTestId("chat-status").textContent).toBe("ready");
  });
  expect(fetchCallCount).toBe(0);
});

test("keeps partial text, raises the fixture error, and never renders the final part", async () => {
  startFixture("error");

  await screen.findByText("Partial before error");
  const alerts = await screen.findAllByRole("alert", undefined, { timeout: 3000 });
  expect(alerts.some((alert) => alert.textContent?.includes("Fixture stream failed"))).toBe(true);
  await waitFor(() => {
    expect(screen.getByTestId("chat-status").textContent).toBe("error");
  });
  expect(screen.getByText("Partial before error")).not.toBeNull();

  await Bun.sleep(100);
  expect(screen.queryByText("Final text must not appear")).toBeNull();
  expect(fetchCallCount).toBe(0);
});

test("approves the local illustrative tool and emits its scripted output", async () => {
  startFixture("approval");

  await screen.findByText("This preview needs your approval.");
  expect(screen.getByTestId("approval-request")).not.toBeNull();
  expect(screen.queryByText(/Draft order #1042/)).toBeNull();

  fireEvent.click(screen.getByRole("button", { name: "Approve" }));

  await screen.findByText("Preview approved.");
  const output = await screen.findByTestId("tool-output");
  expect(output.textContent).toContain("Draft order #1042: 2 add-ons");
  expect(screen.queryByTestId("tool-denied")).toBeNull();
  expect(fetchCallCount).toBe(0);
});

test("denies the local illustrative tool and never emits approved output", async () => {
  startFixture("approval");

  await screen.findByText("This preview needs your approval.");
  fireEvent.click(screen.getByRole("button", { name: "Deny" }));

  await screen.findByText("Preview denied.");
  await screen.findByTestId("tool-denied");
  expect(screen.queryByTestId("tool-output")).toBeNull();
  expect(screen.queryByText(/Draft order #1042/)).toBeNull();
  expect(screen.queryByText("Preview approved.")).toBeNull();
  expect(fetchCallCount).toBe(0);
});
