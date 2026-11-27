using Grpc.Core;
using Teck.Cloud.Commerce.Licensing.Application.Entitlements.Ports;
using OrdersV1 = Teck.Cloud.Commerce.Orders.V1;

namespace Teck.Cloud.Commerce.Licensing.Infrastructure.Clients;

/// <summary>Scoped internal Orders call; every non-OK result fails closed as unavailable.</summary>
/// <param name="client">The managed Orders gRPC client with workload credentials.</param>
/// <param name="clock">The application clock for bounded deadlines.</param>
public sealed class OrdersEntitlementClient(OrdersV1.Orders.OrdersClient client, TimeProvider clock) : IOrdersEntitlementClient
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    /// <inheritdoc/>
    public async Task<EntitlementCheckStatus> CheckAsync(
        string tenantId,
        Guid orderId,
        Guid subscriptionId,
        uint desiredRevision,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(tenantId, "D", out _) || orderId == Guid.Empty || subscriptionId == Guid.Empty || desiredRevision == 0)
        {
            return EntitlementCheckStatus.Unavailable;
        }

        try
        {
            var response = await client.CheckCurrentEntitlementAsync(
                new OrdersV1.CheckCurrentEntitlementRequest
                {
                    TenantId = tenantId,
                    OrderId = orderId.ToString("D"),
                    SubscriptionId = subscriptionId.ToString("D"),
                    DesiredRevision = desiredRevision,
                },
                deadline: clock.GetUtcNow().Add(Deadline).UtcDateTime,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return response.Eligible ? EntitlementCheckStatus.Eligible : EntitlementCheckStatus.NotEligible;
        }
        catch (RpcException)
        {
            return EntitlementCheckStatus.Unavailable;
        }
        catch (HttpRequestException)
        {
            return EntitlementCheckStatus.Unavailable;
        }
        catch (InvalidOperationException)
        {
            return EntitlementCheckStatus.Unavailable;
        }
    }
}
