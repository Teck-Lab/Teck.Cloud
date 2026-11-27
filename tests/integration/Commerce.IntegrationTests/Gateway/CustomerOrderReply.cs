using Grpc.Core;
using Teck.Cloud.Commerce.Orders.V1;

namespace Commerce.IntegrationTests.Gateway;

/// <summary>Scripted Order read outcome.</summary>
internal abstract record CustomerOrderReply
{
    internal static CustomerOrderReply Ok(CustomerOrder order, params string[] trailers) => new Success(order, trailers);

    internal static CustomerOrderReply Error(StatusCode status, params string[] trailers) => new Failed(DependencyFailure.Trailered(status, trailers));

    internal static CustomerOrderReply Failure(DependencyFailure failure) => new Failed(failure);

    internal static CustomerOrderReply TransportFailure() => new Failed(DependencyFailure.Transport());

    internal sealed record Success(CustomerOrder Order, IReadOnlyList<string> Trailers) : CustomerOrderReply;

    internal sealed record Failed(DependencyFailure DependencyError) : CustomerOrderReply;
}
