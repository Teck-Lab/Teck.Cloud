using FastEndpoints.Testing;
using SharedKernel.Api.TestHost;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Owns the endpoint host for the API contract tests.</summary>
[DisableWafCache]
public sealed class ApiApp : AppFixture<ProbeEndpoint>;
