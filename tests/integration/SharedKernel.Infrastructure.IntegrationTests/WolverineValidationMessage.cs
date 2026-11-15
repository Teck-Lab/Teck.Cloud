namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>A message whose name must be present before Wolverine dispatches it.</summary>
/// <param name="Name">The message name.</param>
public sealed record WolverineValidationMessage(string Name);
