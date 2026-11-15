namespace SharedKernel.Api.TestHost;

/// <summary>The successful response from the HTTP validation fixture.</summary>
/// <param name="Name">The accepted request name.</param>
public sealed record ValidationProbeResponse(string Name);
