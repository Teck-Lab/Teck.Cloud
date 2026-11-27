using Teck.Cloud.Commerce.Gateway.Composition;
using Teck.Cloud.Commerce.Gateway.Errors;
using Teck.Cloud.Commerce.Gateway.Models;
using LicensingV1 = Teck.Cloud.Commerce.Licensing.V1;

namespace Teck.Cloud.Commerce.Gateway.Clients;

/// <summary>Maps the customer edge's license read onto the generated Licensing client.</summary>
public sealed class GrpcCommerceLicensingGateway : ICommerceLicensingGateway
{
    private readonly LicensingV1.Licensing.LicensingClient _client;
    private readonly CommerceGatewayOptions _options;

    /// <summary>Initializes a new instance of the <see cref="GrpcCommerceLicensingGateway"/> class.</summary>
    /// <param name="client">The generated Licensing client with exchanged call credentials.</param>
    /// <param name="options">The validated address and deadline.</param>
    public GrpcCommerceLicensingGateway(LicensingV1.Licensing.LicensingClient client, CommerceGatewayOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        _client = client;
        _options = options;
    }

    /// <inheritdoc/>
    public Task<LicenseView> GetCurrentLicenseAsync(string tenantId, Guid orderId, Guid subscriptionId, uint desiredRevision, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        return GetCurrentLicenseCoreAsync(tenantId, orderId, subscriptionId, desiredRevision, cancellationToken);
    }

    private async Task<LicenseView> GetCurrentLicenseCoreAsync(string tenantId, Guid orderId, Guid subscriptionId, uint desiredRevision, CancellationToken cancellationToken)
    {
        if (desiredRevision == 0)
        {
            throw new CommerceDependencyException("Licensing cannot confirm a zero desired generation.");
        }

        var response = await CommerceGrpcCalls.UnaryAsync(
            _client.GetCurrentLicenseAsync(
                new LicensingV1.GetCurrentLicenseRequest
                {
                    TenantId = tenantId,
                    OrderId = orderId.ToString("D"),
                    SubscriptionId = subscriptionId.ToString("D"),
                    DesiredRevision = desiredRevision,
                },
                deadline: DateTime.UtcNow.Add(_options.RequestTimeout),
                cancellationToken: cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return CommerceProtoMapper.ToLicense(response);
    }
}
