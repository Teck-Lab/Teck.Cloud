import { Notice } from "@teck/ui/blocks/notice";
import { TextLink } from "@/components/text-link";

export function FailureNotice({
  kind,
  actionHref = null,
  actionLabel = null,
}: {
  kind: "unauthorized" | "forbidden" | "not_found" | "conflict" | "unavailable" | "error";
  actionHref?: string | null;
  actionLabel?: string | null;
}) {
  const message =
    kind === "forbidden"
      ? "You do not have access to this part of the commerce console."
      : kind === "not_found"
        ? "We couldn't find that item in your organization."
        : kind === "conflict"
          ? "This view is out of date. Start again to load the current data."
          : kind === "unavailable"
            ? "Commerce is temporarily unavailable. Try again shortly."
            : "Something went wrong while loading commerce.";

  return (
    <Notice tone={kind === "not_found" ? "info" : "error"}>
      <span>{message}</span>
      {actionHref === null ? null : (
        <>
          {" "}
          <TextLink href={actionHref}>{actionLabel ?? "Try again"}</TextLink>
        </>
      )}
    </Notice>
  );
}
