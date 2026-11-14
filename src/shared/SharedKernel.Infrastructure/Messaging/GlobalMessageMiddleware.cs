using SharedKernel.Core.MultiTenancy;
using Wolverine;

namespace SharedKernel.Infrastructure.Messaging;

/// <summary>Establishes explicit global scope without inheriting an ambient tenant.</summary>
public static class GlobalMessageMiddleware
{
    /// <summary>Rejects conflicting classification and initializes global data access.</summary>
    /// <param name="envelope">The current message envelope.</param>
    /// <param name="operation">Scoped operation identity.</param>
    /// <param name="context">The current delivery context.</param>
    /// <exception cref="InvalidOperationException">The message has conflicting tenant ownership.</exception>
    public static void Before(Envelope envelope, OperationContext operation, IMessageContext context)
    {
        if (envelope.Message is not IGlobalMessage || envelope.Message is ITenantMessage || !WolverineTenant.IsUnspecified(context.TenantId))
        {
            throw new InvalidOperationException("A global message cannot inherit tenant ownership.");
        }

        operation.Initialize(DataScope.Global);
    }
}
