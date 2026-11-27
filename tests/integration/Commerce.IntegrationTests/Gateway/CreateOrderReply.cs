using Teck.Cloud.Commerce.Orders.V1;

namespace Commerce.IntegrationTests.Gateway;

/// <summary>Scripted checkout reply for the same-intent replay.</summary>
internal abstract record CreateOrderReply
{
    internal static CreateOrderReply Ok(CustomerOrder order, bool hasCheckoutUrl, string? checkoutUrl, params string[] trailers) =>
        new Success(order, hasCheckoutUrl, checkoutUrl, trailers);

    internal static CreateOrderReply Failure(DependencyFailure failure) => new Failed(failure);

    internal static CreateOrderReply TransportFailure() => new Failed(DependencyFailure.Transport());

    internal sealed record Success(CustomerOrder Order, bool HasCheckoutUrl, string? CheckoutUrl, IReadOnlyList<string> Trailers) : CreateOrderReply;

    internal sealed record Failed(DependencyFailure DependencyError) : CreateOrderReply;
}
