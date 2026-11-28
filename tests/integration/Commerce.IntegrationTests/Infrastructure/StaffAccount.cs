namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>A fixture staff principal.</summary>
/// <param name="Username">The Keycloak username.</param>
/// <param name="SubjectId">The access-token subject.</param>
public sealed record StaffAccount(string Username, string SubjectId);
