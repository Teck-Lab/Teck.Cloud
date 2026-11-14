using SharedKernel.Core.MultiTenancy;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Explicit tenant command.</summary>
/// <param name="Id">Record identity.</param>
/// <param name="TenantId">Authorized tenant.</param>
public sealed record CreateTenantRecord(Guid Id, string TenantId) : ITenantMessage;
