namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>A local message used to exercise Wolverine's typed result mapping.</summary>
/// <param name="Id">The value to return on success.</param>
/// <param name="ReturnError">Whether to return a validation error.</param>
public sealed record WolverineResultProbe(Guid Id, bool ReturnError);
