import { afterEach, describe, expect, test } from "bun:test";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { ORDER_ID, REJECTED_ORDER_ID, TENANT_ID } from "@/server/commerce/fixtures.testing";
import type { MutationAction, MutationState } from "./action-state";
import { DecisionForm } from "./decision-form";

afterEach(cleanup);

const REASON = "Representative verified the purchase order and tenant intent.";

function recordingAction(result: MutationState) {
  const calls: FormData[] = [];
  const action: MutationAction = async (_state, formData) => {
    calls.push(formData);
    return result;
  };
  return { action, calls };
}

function reasonField(): HTMLTextAreaElement {
  return screen.getByLabelText("Decision reason") as HTMLTextAreaElement;
}

describe("DecisionForm", () => {
  test("keeps the submitted reason on a recoverable error without replaying the request", async () => {
    const { action, calls } = recordingAction({
      status: "error",
      message: "Enter a reason before recording a decision.",
    });

    render(<DecisionForm tenantId={TENANT_ID} orderId={ORDER_ID} revision={4} action={action} />);

    fireEvent.change(reasonField(), { target: { value: REASON } });
    fireEvent.click(screen.getByRole("button", { name: "Approve" }));

    await waitFor(() => expect(calls).toHaveLength(1));
    expect(calls[0]?.get("decision")).toBe("approve");
    expect(calls[0]?.get("reason")).toBe(REASON);
    expect(calls[0]?.get("tenantId")).toBe(TENANT_ID);
    expect(calls[0]?.get("orderId")).toBe(ORDER_ID);
    expect(calls[0]?.get("expectedRevision")).toBe("4");

    expect(await screen.findByText("Enter a reason before recording a decision.")).toBeTruthy();
    expect(reasonField().value).toBe(REASON);

    await Promise.resolve();
    expect(calls).toHaveLength(1);
  });

  test("drops the recovered draft and result when the order changes", async () => {
    const { action, calls } = recordingAction({
      status: "error",
      message: "Enter a reason before recording a decision.",
    });

    const { rerender } = render(
      <DecisionForm tenantId={TENANT_ID} orderId={ORDER_ID} revision={4} action={action} />,
    );

    fireEvent.change(reasonField(), { target: { value: REASON } });
    fireEvent.click(screen.getByRole("button", { name: "Approve" }));
    await waitFor(() => expect(calls).toHaveLength(1));
    expect(await screen.findByText("Enter a reason before recording a decision.")).toBeTruthy();

    rerender(
      <DecisionForm
        tenantId={TENANT_ID}
        orderId={REJECTED_ORDER_ID}
        revision={4}
        action={action}
      />,
    );

    expect(reasonField().value).toBe("");
    expect(screen.queryByText("Enter a reason before recording a decision.")).toBeNull();
    expect(
      (document.querySelector('input[name="orderId"]') as HTMLInputElement | null)?.value,
    ).toBe(REJECTED_ORDER_ID);
  });
});
