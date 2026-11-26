import { Notice } from "@teck/ui/blocks/notice";
import type { MutationState } from "./action-state";

export function MutationNotice({ state }: { state: MutationState }) {
  if (state.status === "idle") return null;

  if (state.status === "success") {
    return <Notice tone="info">{state.message}</Notice>;
  }

  if (state.status === "conflict") {
    const current = state.current;
    return (
      <Notice tone="error">
        <span>{state.message}</span>
        {current === null ? (
          <span className="mt-1 block">Reload the order before trying again.</span>
        ) : (
          <span className="mt-1 block">
            Current revision {current.revision}; decision {current.approvalStatus}; order status{" "}
            {current.status}.
          </span>
        )}
      </Notice>
    );
  }

  if (state.status === "unauthorized") {
    return (
      <Notice tone="error">
        Your session has expired.{" "}
        <a className="underline" href="/sign-in">
          Sign in again
        </a>{" "}
        before trying again.
      </Notice>
    );
  }

  if (state.status === "forbidden") {
    return <Notice tone="error">You do not have permission to record this change.</Notice>;
  }

  if (state.status === "unavailable") {
    return <Notice tone="error">Commerce is temporarily unavailable. Try again shortly.</Notice>;
  }

  return <Notice tone="error">{state.message}</Notice>;
}
