import { afterEach, describe, expect, it } from "bun:test";
import { cleanup, render, screen } from "@testing-library/react";
import {
  EVIDENCE_REFERENCE,
  makeAdminOrder,
  makePendingApprovalOrder,
  makeRejectedOrder,
  ORDER_ID,
  PLACEMENT_ID,
  TENANT_ID,
} from "@/server/commerce/fixtures.testing";
import type { OrderDetailResult } from "@/server/commerce/orders";
import type { OrderLoadResult } from "@/server/commerce/types";
import type { AdminOrderView } from "@/server/commerce/validate";
import type { MutationAction, MutationState } from "./action-state";
import { MutationNotice } from "./mutation-notice";
import {
  type DetailActions,
  OrderDetailView,
  orderMutationAvailability,
} from "./order-detail-view";

afterEach(cleanup);

const idle: MutationAction = async () => ({ status: "idle" });
const actions: DetailActions = { decide: idle, recordEvidence: idle };

function okResult(order = makeAdminOrder()): OrderLoadResult {
  return { kind: "ok", tenantId: TENANT_ID, orderId: ORDER_ID, order };
}

function viewResult(order: AdminOrderView): OrderDetailResult {
  return { kind: "ok", tenantId: TENANT_ID, orderId: ORDER_ID, order };
}

describe("OrderDetailView", () => {
  it("shows payment, decision, readiness and audit facts", () => {
    const order = makeAdminOrder({
      fulfillment: {
        status: "verifying",
        placementId: PLACEMENT_ID,
        evidenceReference: EVIDENCE_REFERENCE,
        observedAt: null,
        failureCode: "probe_unavailable",
      },
    });
    render(<OrderDetailView result={okResult(order)} actions={actions} />);

    expect(screen.getByRole("heading", { name: "Payment" })).toBeDefined();
    expect(screen.getByRole("heading", { name: "Decision" })).toBeDefined();
    expect(screen.getByRole("heading", { name: "Readiness" })).toBeDefined();
    expect(screen.getByRole("heading", { name: "Audit" })).toBeDefined();
    expect(screen.getAllByText("Paid").length).toBeGreaterThan(0);
    expect(screen.getByText("Probe unavailable; retrying")).toBeDefined();
    expect(screen.getAllByText(EVIDENCE_REFERENCE).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Verified purchase order and tenant intent/).length).toBeGreaterThan(
      0,
    );
  });

  it("offers a reason-required final decision only while approval is pending", () => {
    render(<OrderDetailView result={okResult(makePendingApprovalOrder())} actions={actions} />);

    const reason = screen.getByLabelText("Decision reason") as HTMLTextAreaElement;
    expect(reason.required).toBe(true);
    expect(reason.maxLength).toBe(512);
    expect(screen.getByRole("button", { name: "Approve" })).toBeDefined();
    expect(screen.getByRole("button", { name: "Reject" })).toBeDefined();
    expect(screen.queryByLabelText("Placement UUID")).toBeNull();
  });

  it("shows UUID and manual evidence fields for an approved paid order awaiting readiness", () => {
    render(<OrderDetailView result={okResult(makeAdminOrder())} actions={actions} />);

    const placement = screen.getByLabelText("Placement UUID") as HTMLInputElement;
    expect(placement.pattern).toBe("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}");
    expect(placement.required).toBe(true);
    expect(screen.getByLabelText("Manual evidence reference")).toBeDefined();
    expect(screen.getByRole("button", { name: "Record evidence" })).toBeDefined();
    expect(screen.queryByLabelText("Decision reason")).toBeNull();
    expect(screen.getByText(/never marks the order/)).toBeDefined();
  });

  it("does not offer decision or evidence controls after a final rejection", () => {
    render(<OrderDetailView result={okResult(makeRejectedOrder())} actions={actions} />);

    expect(screen.queryByRole("button", { name: "Approve" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Reject" })).toBeNull();
    expect(screen.queryByLabelText("Placement UUID")).toBeNull();
    expect(screen.getByText(/A final staff decision is recorded/)).toBeDefined();
    expect(
      screen.getAllByText("Purchase order did not match the requesting organization.").length,
    ).toBeGreaterThan(0);
  });

  it("does not force payment or readiness when an order is already active", () => {
    const active = makeAdminOrder({
      status: "active",
      fulfillment: {
        status: "ready",
        placementId: PLACEMENT_ID,
        evidenceReference: EVIDENCE_REFERENCE,
        observedAt: "2026-11-23T20:15:00Z",
        failureCode: null,
      },
    });
    render(<OrderDetailView result={okResult(active)} actions={actions} />);

    expect(screen.queryByRole("button", { name: /force|mark|provision|delete/i })).toBeNull();
    expect(screen.queryByLabelText("Decision reason")).toBeNull();
    expect(screen.queryByLabelText("Placement UUID")).not.toBeNull();
    expect(screen.getByText(/A final staff decision is recorded/)).toBeDefined();
  });

  it("renders an unknown additive order status neutrally without mutation controls", () => {
    const order: AdminOrderView = { ...makeAdminOrder(), status: "pendingReview" };
    render(<OrderDetailView result={viewResult(order)} actions={actions} />);

    expect(screen.getByText("Unknown")).toBeDefined();
    expect(screen.getByText(/does not recognise/)).toBeDefined();
    expect(screen.queryByRole("button", { name: "Approve" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Reject" })).toBeNull();
    expect(screen.queryByLabelText("Placement UUID")).toBeNull();
    expect(screen.queryByText(/A final staff decision is recorded/)).toBeNull();
    expect(screen.queryByText(/becomes available after payment/)).toBeNull();
  });

  it("fails closed when a nested status is additive", () => {
    const base = makeAdminOrder({ status: "pendingApproval" });
    const order: AdminOrderView = {
      ...base,
      approval: { ...base.approval, status: "escalated" },
    };

    expect(orderMutationAvailability(order)).toEqual({
      knownOrderStatus: true,
      knownApprovalStatus: false,
      canDecide: false,
      canRecordEvidence: false,
    });

    render(<OrderDetailView result={viewResult(order)} actions={actions} />);

    expect(screen.getByText(/does not recognise/)).toBeDefined();
    expect(screen.queryByRole("button", { name: "Approve" })).toBeNull();
    expect(screen.queryByLabelText("Placement UUID")).toBeNull();
    expect(screen.queryByText(/A final staff decision is recorded/)).toBeNull();
  });

  it("renders expired, denied, missing and unavailable states without staff secrets", () => {
    render(<OrderDetailView result={{ kind: "unauthorized" }} actions={actions} />);
    expect(screen.getByText(/session has expired/i)).toBeDefined();
    expect(screen.getByRole("link", { name: "Sign in" }).getAttribute("href")).toBe("/sign-in");
    cleanup();

    render(<OrderDetailView result={{ kind: "forbidden" }} actions={actions} />);
    expect(screen.getByText(/do not have permission/i)).toBeDefined();
    cleanup();

    render(<OrderDetailView result={{ kind: "notFound" }} actions={actions} />);
    expect(screen.getByText(/not available for the selected tenant/i)).toBeDefined();
    cleanup();

    render(<OrderDetailView result={{ kind: "unavailable" }} actions={actions} />);
    expect(screen.getByText(/temporarily unavailable/i)).toBeDefined();

    const text = document.body.textContent ?? "";
    expect(text).not.toContain("license");
    expect(text).not.toContain("provider");
    expect(text).not.toContain("fixture-token");
    expect(text).not.toMatch(/force[- ]?(paid|ready)/i);
  });

  it("never renders a license envelope or provider reference in a successful detail", () => {
    render(<OrderDetailView result={okResult()} actions={actions} />);

    const text = document.body.textContent ?? "";
    expect(text).not.toContain("license");
    expect(text).not.toContain("provider");
    expect(text).not.toContain("checkout");
  });

  it("reports a conflict with the freshly read revision and no success claim", () => {
    const state: MutationState = {
      status: "conflict",
      message: "This order changed while you were reviewing it.",
      current: {
        revision: 6,
        status: "pendingReadiness",
        approvalStatus: "approved",
      },
    };
    render(<MutationNotice state={state} />);

    expect(screen.getByText(/Current revision 6/)).toBeDefined();
    expect(screen.queryByText(/Decision recorded/)).toBeNull();
  });

  it("reports a recorded decision once", () => {
    const state: MutationState = {
      status: "success",
      message: "Decision recorded.",
    };
    render(<MutationNotice state={state} />);

    expect(screen.getByText("Decision recorded.")).toBeDefined();
  });
});
