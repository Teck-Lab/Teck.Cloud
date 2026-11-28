"use client";

import { Button } from "@teck/ui/components/button";
import { Field } from "@teck/ui/components/field";
import { useActionState, useEffect, useState } from "react";
import { idleMutationState, type MutationAction } from "./action-state";
import { MutationNotice } from "./mutation-notice";

type DecisionFormProps = {
  tenantId: string;
  orderId: string;
  revision: number;
  action: MutationAction;
};

const reasonClass =
  "min-h-24 w-full rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground shadow-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring";

export function DecisionForm(props: DecisionFormProps) {
  // A new order must not inherit a recovered draft or result from the previous one.
  return <DecisionFormFields key={`${props.tenantId}/${props.orderId}`} {...props} />;
}

function DecisionFormFields({ tenantId, orderId, revision, action }: DecisionFormProps) {
  const [state, formAction, pending] = useActionState(action, idleMutationState);
  const [reason, setReason] = useState("");

  useEffect(() => {
    if (state.status === "success") {
      setReason("");
    }
  }, [state]);

  return (
    <form action={formAction} className="flex flex-col gap-3">
      <input type="hidden" name="tenantId" value={tenantId} />
      <input type="hidden" name="orderId" value={orderId} />
      <input type="hidden" name="expectedRevision" value={revision} />

      <Field label="Decision reason" htmlFor="decision-reason">
        <textarea
          id="decision-reason"
          name="reason"
          required
          maxLength={512}
          rows={3}
          aria-describedby="decision-reason-help"
          className={reasonClass}
          value={reason}
          onChange={(event) => setReason(event.target.value)}
        />
      </Field>
      <p id="decision-reason-help" className="text-xs text-muted-foreground">
        A trimmed reason is required and becomes part of the audit record. The first decision is
        final.
      </p>

      <div className="flex flex-wrap items-center gap-2">
        <Button type="submit" name="decision" value="approve" disabled={pending}>
          Approve
        </Button>
        <Button type="submit" name="decision" value="reject" disabled={pending}>
          Reject
        </Button>
      </div>

      <div aria-live="polite">
        <MutationNotice state={state} />
      </div>
    </form>
  );
}
