using SharedKernel.Core.MultiTenancy;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Records broker delivery using the validated envelope identity.</summary>
public static class TenantRecordPublishedHandler
{
    /// <summary>Stages a tenant-isolated delivery receipt.</summary>
    /// <param name="message">The external event.</param>
    /// <param name="operation">Identity established from Wolverine's envelope.</param>
    /// <param name="context">The consumer's isolated write context.</param>
    public static void Handle(TenantRecordPublished message, OperationContext operation, MessageDbContext context) =>
        _ = context.Add(new TenantReceipt { Id = message.RecordId, TenantId = operation.Scope.TenantId! });
}
