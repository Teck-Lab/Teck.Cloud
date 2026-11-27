using System.Text.Json;
using ErrorOr;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Keycloak.AuthServices.Common.Claims;
using Microsoft.AspNetCore.Authorization;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Licensing.Application.Entitlements.Features.GetCurrentLicense.V1;
using Teck.Cloud.Commerce.Licensing.Application.Entitlements.Features.VerifyLicense.V1;
using Teck.Cloud.Commerce.Licensing.Application.Entitlements.ReadModels;
using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGrants;
using Teck.Cloud.Commerce.Licensing.V1;
using Wolverine;
using LicensingService = Teck.Cloud.Commerce.Licensing.V1.Licensing;
using ProtoRejection = Teck.Cloud.Commerce.Licensing.V1.RejectionCode;
using ProtoTier = Teck.Cloud.Commerce.Licensing.V1.Tier;

namespace Teck.Cloud.Commerce.Licensing.Host;

/// <summary>Internal workload gRPC surface; only authenticated Commerce licensers are authorized.</summary>
/// <param name="bus">Wolverine request/response dispatch.</param>
/// <param name="operation">The tenant scope established by the shared Commerce interceptor.</param>
[Authorize(Policy = CommerceAuth.LicenseReadPolicy)]
public sealed class LicensingGrpcService(IMessageBus bus, OperationContext operation) : LicensingService.LicensingBase
{
    /// <inheritdoc/>
    public override Task<GetCurrentLicenseResponse> GetCurrentLicense(GetCurrentLicenseRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        return GetCurrentLicenseAsync(request, context);
    }

    /// <inheritdoc/>
    public override Task<VerifyLicenseResponse> VerifyLicense(VerifyLicenseRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        return VerifyLicenseAsync(request, context);
    }

    private static Guid RequireUuid(string value) =>
        Guid.TryParseExact(value, "D", out var parsed)
            ? parsed
            : throw CommerceRpc.Error(CommerceErrorCodes.InvalidRequest);

    private static ProtoTier MapTier(LicenseTier tier) => tier switch
    {
        LicenseTier.Shared => ProtoTier.Shared,
        LicenseTier.Dedicated => ProtoTier.Dedicated,
        _ => ProtoTier.Unspecified,
    };

    private static ProtoRejection MapRejection(LicenseRejection rejection) => rejection switch
    {
        LicenseRejection.None => ProtoRejection.Unspecified,
        LicenseRejection.InvalidSignature => ProtoRejection.InvalidSignature,
        LicenseRejection.Expired => ProtoRejection.Expired,
        LicenseRejection.Revoked => ProtoRejection.Revoked,
        LicenseRejection.WrongBinding => ProtoRejection.WrongBinding,
        LicenseRejection.UnknownKey => ProtoRejection.UnknownKey,
        _ => ProtoRejection.Unspecified,
    };

    private static RpcException Failure(List<Error> errors)
    {
        var code = errors.Count == 0 ? CommerceErrorCodes.DependencyUnavailable : errors[0].Code;
        return code switch
        {
            CommerceErrorCodes.InvalidRequest => CommerceRpc.Error(CommerceErrorCodes.InvalidRequest),
            CommerceErrorCodes.NotFound => CommerceRpc.Error(CommerceErrorCodes.NotFound),
            CommerceErrorCodes.Forbidden => CommerceRpc.Error(CommerceErrorCodes.Forbidden),
            _ => CommerceRpc.Error(CommerceErrorCodes.DependencyUnavailable),
        };
    }

    private static bool HasMismatchedOrganization(ServerCallContext context, string tenantId)
    {
        try
        {
            var organizations = context.GetHttpContext().User.GetOrganizations();
            return organizations.Count > 0
                && (organizations.Count != 1 || !string.Equals(organizations[0].Id, tenantId, StringComparison.Ordinal));
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private async Task<GetCurrentLicenseResponse> GetCurrentLicenseAsync(GetCurrentLicenseRequest request, ServerCallContext context)
    {
        var tenantId = RequireTenant(request.TenantId, context);
        var orderId = RequireUuid(request.OrderId);
        var subscriptionId = RequireUuid(request.SubscriptionId);
        var result = await bus.InvokeForTenantAsync<ErrorOr<LicenseEnvelopeView>>(
            tenantId,
            new GetCurrentLicenseQuery(tenantId, orderId, subscriptionId, request.DesiredRevision),
            context.CancellationToken).ConfigureAwait(false);
        return result.IsError
            ? throw Failure(result.Errors)
            : new GetCurrentLicenseResponse
            {
                License = new LicenseEnvelope
                {
                    Document = result.Value.Document,
                    KeyId = result.Value.KeyId,
                    Revision = result.Value.Revision,
                    ExpiresAt = Timestamp.FromDateTimeOffset(result.Value.ExpiresAt),
                },
            };
    }

    private async Task<VerifyLicenseResponse> VerifyLicenseAsync(VerifyLicenseRequest request, ServerCallContext context)
    {
        var tenantId = RequireTenant(request.ExpectedTenantId, context);
        var result = await bus.InvokeForTenantAsync<ErrorOr<LicenseVerificationView>>(
            tenantId,
            new VerifyLicenseQuery(tenantId, request.Document, tenantId, request.ExpectedSubscriptionId),
            context.CancellationToken).ConfigureAwait(false);
        if (result.IsError)
        {
            throw Failure(result.Errors);
        }

        var view = result.Value;
        var response = new VerifyLicenseResponse
        {
            Valid = view.Valid,
            Rejection = MapRejection(view.Rejection),
        };
        if (view.Claims is { } claims)
        {
            response.Claims = new LicenseClaims
            {
                TenantId = claims.TenantId,
                OrderId = claims.OrderId.ToString("D"),
                SubscriptionId = claims.SubscriptionId.ToString("D"),
                Tier = MapTier(claims.Tier),
                Revision = claims.Revision,
                KeyId = claims.KeyId,
                ExpiresAt = Timestamp.FromDateTimeOffset(claims.ExpiresAt),
            };
            response.Claims.AddOnCodes.AddRange(claims.AddOnCodes);
        }

        return response;
    }

    private string RequireTenant(string requestedTenantId, ServerCallContext context)
    {
        if (!Guid.TryParseExact(requestedTenantId, "D", out var requested))
        {
            throw CommerceRpc.Error(CommerceErrorCodes.InvalidRequest);
        }

        string scopeTenant;
        try
        {
            scopeTenant = operation.Scope.TenantId ?? string.Empty;
        }
        catch (InvalidOperationException)
        {
            scopeTenant = string.Empty;
        }

        var canonical = requested.ToString("D");
        return string.Equals(scopeTenant, canonical, StringComparison.Ordinal)
            && !HasMismatchedOrganization(context, canonical)
            ? canonical
            : throw CommerceRpc.Error(CommerceErrorCodes.Forbidden);
    }
}
