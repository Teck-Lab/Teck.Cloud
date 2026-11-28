"use client";

import { Button } from "@teck/ui/components/button";
import { Field } from "@teck/ui/components/field";
import { Input } from "@teck/ui/components/input";
import { useActionState, useEffect, useState } from "react";
import { idleMutationState, type MutationAction } from "./action-state";
import { MutationNotice } from "./mutation-notice";

type EvidenceFormProps = {
  tenantId: string;
  orderId: string;
  revision: number;
  action: MutationAction;
};

export function EvidenceForm(props: EvidenceFormProps) {
  // A new order must not inherit a recovered draft or result from the previous one.
  return <EvidenceFormFields key={`${props.tenantId}/${props.orderId}`} {...props} />;
}

function EvidenceFormFields({ tenantId, orderId, revision, action }: EvidenceFormProps) {
  const [state, formAction, pending] = useActionState(action, idleMutationState);
  const [placementId, setPlacementId] = useState("");
  const [evidenceReference, setEvidenceReference] = useState("");

  useEffect(() => {
    if (state.status === "success") {
      setPlacementId("");
      setEvidenceReference("");
    }
  }, [state]);

  return (
    <form action={formAction} className="flex flex-col gap-3">
      <input type="hidden" name="tenantId" value={tenantId} />
      <input type="hidden" name="orderId" value={orderId} />
      <input type="hidden" name="expectedRevision" value={revision} />

      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Placement UUID" htmlFor="placement-id">
          <Input
            id="placement-id"
            name="placementId"
            required
            autoComplete="off"
            spellCheck={false}
            pattern="[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}"
            placeholder="00000000-0000-0000-0000-000000000000"
            value={placementId}
            onChange={(event) => setPlacementId(event.target.value)}
          />
        </Field>
        <Field label="Manual evidence reference" htmlFor="evidence-reference">
          <Input
            id="evidence-reference"
            name="evidenceReference"
            required
            maxLength={512}
            autoComplete="off"
            spellCheck={false}
            placeholder="evidence://ops/ticket-1234"
            value={evidenceReference}
            onChange={(event) => setEvidenceReference(event.target.value)}
          />
        </Field>
      </div>
      <p className="text-xs text-muted-foreground">
        The reference is stored as audit evidence and is not fetched by this page. Verification
        remains asynchronous; recording evidence never marks the order ready.
      </p>

      <div className="flex flex-wrap items-center gap-2">
        <Button type="submit" disabled={pending}>
          Record evidence
        </Button>
      </div>

      <div aria-live="polite">
        <MutationNotice state={state} />
      </div>
    </form>
  );
}
