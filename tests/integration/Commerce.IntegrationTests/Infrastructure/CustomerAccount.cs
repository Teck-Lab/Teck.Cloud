namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>A real customer identity in one fixture tenant.</summary>
/// <param name="TenantId">The immutable organization identifier.</param>
/// <param name="SubjectId">The access-token subject.</param>
public sealed record CustomerAccount(Guid TenantId, string SubjectId);
