using ErrorOr;
using SharedKernel.Core.MultiTenancy;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Verifies generated middleware and EF use one scoped identity.</summary>
public static class CreateTenantRecordHandler
{
    /// <summary>Creates a tenant-owned row using established identity.</summary>
    /// <param name="command">The command.</param>
    /// <param name="operation">Validated operation identity.</param>
    /// <param name="context">The same scoped data adapter.</param>
    /// <returns>The created identifier.</returns>
    public static ErrorOr<Guid> Handle(CreateTenantRecord command, OperationContext operation, MessageDbContext context)
    {
        _ = context.Add(new TenantRecord(command.Id, operation.Scope.TenantId!));
        return command.Id;
    }
}
