using System.Collections.Frozen;
using System.Security.Claims;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Core.MultiTenancy;

namespace Teck.Cloud.Commerce.Contracts;

/// <summary>Establishes one validated tenant scope for commerce gRPC handlers after endpoint authorization.</summary>
public sealed class CommerceScopeInterceptor : Interceptor
{
    private static readonly FrozenSet<string> StaffPolicies =
        new HashSet<string>(StringComparer.Ordinal)
        {
            CommerceAuth.OrdersReadPolicy,
            CommerceAuth.OrdersDecidePolicy,
            CommerceAuth.PlacementRecordPolicy,
            CommerceAuth.EntitlementCheckPolicy,
            CommerceAuth.LicenseReadPolicy,
        }.ToFrozenSet(StringComparer.Ordinal);

    /// <inheritdoc/>
    public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(continuation);
        return InvokeAsync();

        async Task<TResponse> InvokeAsync()
        {
            await EstablishAsync(context).ConfigureAwait(false);
            return await continuation(request, context).ConfigureAwait(false);
        }
    }

    private static async Task EstablishAsync(ServerCallContext context)
    {
        var http = context.GetHttpContext();
        if (http.User.Identity?.IsAuthenticated != true)
        {
            throw CommerceRpc.Error(CommerceErrorCodes.Unauthenticated);
        }

        var policies = http.GetEndpoint()?.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Select(metadata => metadata.Policy)
            .Where(policy => !string.IsNullOrWhiteSpace(policy))
            .Select(policy => policy!)
            .ToArray() ?? [];
        var metadataTenant = TenantFrom(context.RequestHeaders);
        var customer = policies.Contains(CommerceAuth.CustomerPolicy, StringComparer.Ordinal);
        string tenant;
        if (customer)
        {
            if (policies.Any(policy => !string.Equals(policy, CommerceAuth.CustomerPolicy, StringComparison.Ordinal)))
            {
                throw CommerceRpc.Error(CommerceErrorCodes.Forbidden);
            }

            tenant = SelectCustomerTenant(http);
            if (tenant.Length == 0)
            {
                throw CommerceRpc.Error(CommerceErrorCodes.Forbidden);
            }

            if (metadataTenant is not null && !string.Equals(metadataTenant, tenant, StringComparison.Ordinal))
            {
                throw CommerceRpc.Error(CommerceErrorCodes.Forbidden);
            }
        }
        else
        {
            if (policies.Length == 0 || !policies.All(StaffPolicies.Contains))
            {
                throw CommerceRpc.Error(CommerceErrorCodes.Forbidden);
            }

            tenant = metadataTenant ?? throw CommerceRpc.Error(CommerceErrorCodes.InvalidRequest);
        }

        var services = http.RequestServices;
        var directory = services.GetService<ITenantDirectory>()
            ?? throw new InvalidOperationException("Commerce gRPC handlers require a registered ITenantDirectory.");
        if (!await directory.IsActiveAsync(tenant, context.CancellationToken).ConfigureAwait(false))
        {
            throw CommerceRpc.Error(CommerceErrorCodes.Forbidden);
        }

        services.GetRequiredService<OperationContext>().Initialize(DataScope.ForTenant(tenant), http.User.FindFirstValue("sub"));
        _ = System.Diagnostics.Activity.Current?.SetTag("tenant.id", tenant);
    }

    private static string SelectCustomerTenant(HttpContext http)
    {
        try
        {
            return CommerceAuth.SelectedOrganizationId(http);
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
    }

    private static string? TenantFrom(Metadata headers)
    {
        var values = headers.Where(entry => string.Equals(entry.Key, CommerceAuth.TenantMetadataKey, StringComparison.Ordinal)).ToArray();
        return values.Length == 1 && Guid.TryParseExact(values[0].Value, "D", out var tenant)
            ? tenant.ToString("D")
            : null;
    }
}
