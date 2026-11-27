using Teck.Cloud.Commerce.Orders.V1;

namespace Commerce.IntegrationTests.Gateway;

/// <summary>Scripted Orders page outcome.</summary>
internal abstract record ListOrdersReply
{
    internal static ListOrdersReply Ok(IReadOnlyList<CustomerOrder> items, string? nextCursor, params string[] trailers) =>
        new Success(items, HasNextCursor: true, nextCursor, trailers);

    internal static ListOrdersReply OkWithoutCursor(IReadOnlyList<CustomerOrder> items) =>
        new Success(items, HasNextCursor: false, NextCursor: null, []);

    internal static ListOrdersReply Failure(DependencyFailure failure) => new Failed(failure);

    internal static ListOrdersReply TransportFailure() => new Failed(DependencyFailure.Transport());

    internal sealed record Success(IReadOnlyList<CustomerOrder> Items, bool HasNextCursor, string? NextCursor, IReadOnlyList<string> Trailers) : ListOrdersReply;

    internal sealed record Failed(DependencyFailure DependencyError) : ListOrdersReply;
}
