using System.Collections.Concurrent;
using Grpc.Core;

namespace Commerce.IntegrationTests.Gateway;

/// <summary>Holds scripted replies for the fake Orders and Licensing services.</summary>
internal sealed class FakeCommerceScript
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<CustomerOrderReply>> _orders = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<LicenseReply>> _licenses = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<ListOrdersReply> _pages = new();
    private readonly ConcurrentQueue<CreateOrderReply> _creates = new();

    internal FakeCommerceScript Order(string orderId, CustomerOrderReply reply)
    {
        _orders.GetOrAdd(orderId, static _ => new ConcurrentQueue<CustomerOrderReply>()).Enqueue(reply);
        return this;
    }

    internal FakeCommerceScript License(string orderId, uint desiredRevision, LicenseReply reply)
    {
        _licenses.GetOrAdd(Key(orderId, desiredRevision), static _ => new ConcurrentQueue<LicenseReply>()).Enqueue(reply);
        return this;
    }

    internal FakeCommerceScript Page(ListOrdersReply reply)
    {
        _pages.Enqueue(reply);
        return this;
    }

    internal FakeCommerceScript Create(CreateOrderReply reply)
    {
        _creates.Enqueue(reply);
        return this;
    }

    internal CustomerOrderReply NextOrder(string orderId) =>
        _orders.TryGetValue(orderId, out var replies) && replies.TryDequeue(out var reply)
            ? reply
            : throw new RpcException(new Status(StatusCode.Internal, "Unscripted GetCustomerOrder."));

    internal LicenseReply NextLicense(string orderId, uint desiredRevision) =>
        _licenses.TryGetValue(Key(orderId, desiredRevision), out var replies) && replies.TryDequeue(out var reply)
            ? reply
            : throw new RpcException(new Status(StatusCode.Internal, "Unscripted GetCurrentLicense."));

    internal ListOrdersReply NextPage() =>
        _pages.TryDequeue(out var reply)
            ? reply
            : throw new RpcException(new Status(StatusCode.Internal, "Unscripted ListCustomerOrders."));

    internal CreateOrderReply NextCreate() =>
        _creates.TryDequeue(out var reply)
            ? reply
            : throw new RpcException(new Status(StatusCode.Internal, "Unscripted CreateOrder."));

    private static string Key(string orderId, uint desiredRevision) => orderId + "/" + desiredRevision.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
