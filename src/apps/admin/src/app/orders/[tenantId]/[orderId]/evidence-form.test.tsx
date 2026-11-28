import { afterEach, describe, expect, test } from "bun:test";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import {
  EVIDENCE_REFERENCE,
  ORDER_ID,
  PLACEMENT_ID,
  REJECTED_ORDER_ID,
  TENANT_ID,
} from "@/server/commerce/fixtures.testing";
import type { MutationAction, MutationState } from "./action-state";
import { EvidenceForm } from "./evidence-form";

afterEach(cleanup);

function recordingAction(result: MutationState) {
  const calls: FormData[] = [];
  const action: MutationAction = async (_state, formData) => {
    calls.push(formData);
    return result;
  };
  return { action, calls };
}

function placementField(): HTMLInputElement {
  return screen.getByLabelText("Placement UUID") as HTMLInputElement;
}

function referenceField(): HTMLInputElement {
  return screen.getByLabelText("Manual evidence reference") as HTMLInputElement;
}

describe("EvidenceForm", () => {
  test("keeps the submitted evidence on a recoverable error without replaying the request", async () => {
    const { action, calls } = recordingAction({
      status: "error",
      message: "Enter the placement UUID.",
    });

    render(<EvidenceForm tenantId={TENANT_ID} orderId={ORDER_ID} revision={6} action={action} />);

    fireEvent.change(placementField(), { target: { value: PLACEMENT_ID } });
    fireEvent.change(referenceField(), { target: { value: EVIDENCE_REFERENCE } });
    fireEvent.click(screen.getByRole("button", { name: "Record evidence" }));

    await waitFor(() => expect(calls).toHaveLength(1));
    expect(calls[0]?.get("placementId")).toBe(PLACEMENT_ID);
    expect(calls[0]?.get("evidenceReference")).toBe(EVIDENCE_REFERENCE);
    expect(calls[0]?.get("tenantId")).toBe(TENANT_ID);
    expect(calls[0]?.get("orderId")).toBe(ORDER_ID);
    expect(calls[0]?.get("expectedRevision")).toBe("6");

    expect(await screen.findByText("Enter the placement UUID.")).toBeTruthy();
    expect(placementField().value).toBe(PLACEMENT_ID);
    expect(referenceField().value).toBe(EVIDENCE_REFERENCE);

    await Promise.resolve();
    expect(calls).toHaveLength(1);
  });

  test("drops the recovered evidence when the order changes", async () => {
    const { action, calls } = recordingAction({
      status: "error",
      message: "Enter the placement UUID.",
    });

    const { rerender } = render(
      <EvidenceForm tenantId={TENANT_ID} orderId={ORDER_ID} revision={6} action={action} />,
    );

    fireEvent.change(placementField(), { target: { value: PLACEMENT_ID } });
    fireEvent.change(referenceField(), { target: { value: EVIDENCE_REFERENCE } });
    fireEvent.click(screen.getByRole("button", { name: "Record evidence" }));
    await waitFor(() => expect(calls).toHaveLength(1));
    expect(await screen.findByText("Enter the placement UUID.")).toBeTruthy();

    rerender(
      <EvidenceForm
        tenantId={TENANT_ID}
        orderId={REJECTED_ORDER_ID}
        revision={6}
        action={action}
      />,
    );

    expect(placementField().value).toBe("");
    expect(referenceField().value).toBe("");
    expect(screen.queryByText("Enter the placement UUID.")).toBeNull();
    expect(
      (document.querySelector('input[name="orderId"]') as HTMLInputElement | null)?.value,
    ).toBe(REJECTED_ORDER_ID);
  });
});
