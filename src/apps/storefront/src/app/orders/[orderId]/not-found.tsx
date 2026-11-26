import { PageFrame, PageHeading } from "@/components/page-frame";
import { TextLink } from "@/components/text-link";

export default function OrderNotFound() {
  return (
    <PageFrame>
      <PageHeading
        eyebrow="Commerce"
        title="Order not found"
        description="This order does not exist in your organization, or it is no longer available to this session."
      />
      <p className="text-sm text-muted-foreground">
        Any checkout recovery record stored in this browser is left untouched.
      </p>
      <div className="mt-4 flex flex-wrap gap-4">
        <TextLink href="/basket">Back to basket</TextLink>
        <TextLink href="/orders">Order history</TextLink>
      </div>
    </PageFrame>
  );
}
