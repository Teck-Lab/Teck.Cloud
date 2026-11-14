using System.Diagnostics;
using SharedKernel.Core.MultiTenancy;
using Wolverine;

namespace SharedKernel.Infrastructure.Messaging;

/// <summary>Establishes tenant scope for messages received from authenticated, authorized publishers.</summary>
public static class TenantMessageMiddleware
{
    /// <summary>Validates ownership before dependencies use tenant data.</summary>
    /// <param name="envelope">The current message envelope.</param>
    /// <param name="operation">Scoped operation identity.</param>
    /// <param name="directory">The authoritative active-tenant lookup.</param>
    /// <param name="context">Wolverine message context.</param>
    /// <param name="cancellationToken">Delivery cancellation.</param>
    /// <returns>The validation task.</returns>
    /// <exception cref="InvalidOperationException">The tenant scope or its resolver is invalid.</exception>
    public static async Task BeforeAsync(Envelope envelope, OperationContext operation, ITenantDirectory directory, IMessageContext context, CancellationToken cancellationToken)
    {
        if (envelope.Message is not ITenantMessage message || message is IGlobalMessage || string.IsNullOrWhiteSpace(message.TenantId) || WolverineTenant.IsUnspecified(message.TenantId)
            || WolverineTenant.IsUnspecified(context.TenantId) || !string.Equals(context.TenantId, message.TenantId, StringComparison.Ordinal)
            || !await directory.IsActiveAsync(message.TenantId, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Message tenant scope is missing, inactive or inconsistent.");
        }

        operation.Initialize(DataScope.ForTenant(message.TenantId));
        _ = Activity.Current?.SetTag("tenant.id", message.TenantId);
    }
}
