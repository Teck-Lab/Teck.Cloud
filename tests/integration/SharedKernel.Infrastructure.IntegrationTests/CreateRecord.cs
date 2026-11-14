using SharedKernel.Core.MultiTenancy;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Explicit global command.</summary>
/// <param name="Id">Record identity.</param>
/// <param name="Fail">Whether to fail the transaction.</param>
public sealed record CreateRecord(Guid Id, bool Fail) : IGlobalMessage;
