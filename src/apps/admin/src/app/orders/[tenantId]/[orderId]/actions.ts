"use server";

import { revalidatePath } from "next/cache";
import { headers } from "next/headers";
import { adminCommerce } from "@/server/commerce/runtime";
import {
  MAX_EVIDENCE_REFERENCE_LENGTH,
  MAX_REASON_LENGTH,
  MESSAGES,
  type MutationResult,
} from "@/server/commerce/types";
import type { MutationState } from "./action-state";

function field(formData: FormData, name: string): string {
  const value = formData.get(name);
  return typeof value === "string" ? value : "";
}

function toState(result: MutationResult, successMessage: string): MutationState {
  switch (result.kind) {
    case "updated":
      return { status: "success", message: successMessage };
    case "conflict":
      return {
        status: "conflict",
        message:
          "This order changed while you were reviewing it. The current state has been read again; review it before trying again.",
        current:
          result.order === null
            ? null
            : {
                revision: result.order.revision,
                status: result.order.status,
                approvalStatus: result.order.approval.status,
              },
      };
    case "invalid":
      return { status: "error", message: result.message };
    case "unauthorized":
      return { status: "unauthorized" };
    case "forbidden":
      return { status: "forbidden" };
    case "notFound":
      return { status: "error", message: MESSAGES.notFound };
    case "unavailable":
      return { status: "unavailable" };
    case "error":
      return { status: "error", message: result.message };
  }
}

export async function decideOrderAction(
  _state: MutationState,
  formData: FormData,
): Promise<MutationState> {
  const tenantId = field(formData, "tenantId");
  const orderId = field(formData, "orderId");
  const decision = field(formData, "decision");
  const reason = field(formData, "reason").trim();
  const expectedRevision = Number(field(formData, "expectedRevision"));

  if (decision !== "approve" && decision !== "reject") {
    return { status: "error", message: "Choose approve or reject." };
  }
  if (reason.length === 0) {
    return { status: "error", message: "Enter a reason before recording a decision." };
  }
  if (reason.length > MAX_REASON_LENGTH) {
    return {
      status: "error",
      message: `The reason must be at most ${MAX_REASON_LENGTH} characters.`,
    };
  }

  const result = await adminCommerce().decideOrder(await headers(), {
    tenantId,
    orderId,
    decision,
    reason,
    expectedRevision,
  });
  if (result.kind === "updated" || result.kind === "conflict") {
    revalidatePath(`/orders/${tenantId}/${orderId}`);
  }
  return toState(result, "Decision recorded.");
}

export async function recordPlacementEvidenceAction(
  _state: MutationState,
  formData: FormData,
): Promise<MutationState> {
  const tenantId = field(formData, "tenantId");
  const orderId = field(formData, "orderId");
  const placementId = field(formData, "placementId").trim();
  const evidenceReference = field(formData, "evidenceReference").trim();
  const expectedRevision = Number(field(formData, "expectedRevision"));

  if (placementId.length === 0) {
    return { status: "error", message: "Enter the placement UUID." };
  }
  if (evidenceReference.length === 0) {
    return { status: "error", message: "Enter a manual evidence reference." };
  }
  if (evidenceReference.length > MAX_EVIDENCE_REFERENCE_LENGTH) {
    return {
      status: "error",
      message: `The evidence reference must be at most ${MAX_EVIDENCE_REFERENCE_LENGTH} characters.`,
    };
  }

  const result = await adminCommerce().recordPlacementEvidence(await headers(), {
    tenantId,
    orderId,
    placementId,
    evidenceReference,
    expectedRevision,
  });
  if (result.kind === "updated" || result.kind === "conflict") {
    revalidatePath(`/orders/${tenantId}/${orderId}`);
  }
  return toState(result, "Placement evidence recorded.");
}
