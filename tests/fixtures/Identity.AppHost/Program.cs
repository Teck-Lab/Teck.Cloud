using Teck.Cloud.AppHost;

namespace Identity.AppHost;

/// <summary>Owns an isolated assembled identity boundary using the production dependency composition.</summary>
public static class Program
{
    /// <summary>Runs the browser gateway, HTTP/2 service and real identity/persistence dependencies.</summary>
    /// <param name="args">Fixture settings and generated secrets.</param>
    /// <returns>The application lifetime.</returns>
    public static async Task Main(string[] args)
    {
        var builder = DistributedApplication.CreateBuilder(args);
        var repository = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", ".."));
        builder.AddFoundationServices(Path.Combine(repository, "src", "aspire", "Teck.Cloud.AppHost", "Identity"));
        var redis = builder.AddFoundationRedisSentinel();
        var postgres = builder.CreateResourceBuilder(builder.Resources.OfType<PostgresServerResource>().Single());
        var identityDatabase = postgres.AddDatabase("identity-db");
        var keycloak = builder.CreateResourceBuilder(builder.Resources.OfType<KeycloakResource>().Single());
        var secret = builder.AddParameter("identity-fixture-secret", secret: true);
        var readerSecret = builder.Resources.OfType<ParameterResource>().Single(parameter => string.Equals(parameter.Name, "keycloak-tenant-reader-secret", StringComparison.Ordinal));
        var service = builder.AddProject<Projects.Identity_TestHost>("identity-service")
            .WithHttpEndpoint(name: "grpc", isProxied: false)
            .WithEnvironment("IDENTITY_ROLE", "Service")
            .WithEnvironment("Kestrel__EndpointDefaults__Protocols", "Http2")
            .WithEnvironment("Identity__ServerUrl", keycloak.GetEndpoint("http"))
            .WithEnvironment("Identity__ClientSecret", secret)
            .WithEnvironment("Identity__ReaderSecret", readerSecret)
            .WithReference(identityDatabase)
            .WithReference(redis)
            .WaitFor(keycloak)
            .WaitFor(identityDatabase);
        _ = builder.AddProject<Projects.Identity_TestHost>("identity-gateway")
            .WithHttpEndpoint(name: "http")
            .WithHttpHealthCheck("/healthz", endpointName: "http")
            .WithEnvironment("IDENTITY_ROLE", "Gateway")
            .WithEnvironment("Identity__ServerUrl", keycloak.GetEndpoint("http"))
            .WithEnvironment("Identity__ClientSecret", secret)
            .WithEnvironment("Identity__ReaderSecret", readerSecret)
            .WithEnvironment("Identity__DownstreamUrl", service.GetEndpoint("grpc"))
            .WithReference(redis)
            .WaitFor(keycloak)
            .WaitFor(service);
        _ = builder.AddProject<Projects.Identity_TestHost>("identity-admin-gateway")
            .WithHttpEndpoint(name: "http")
            .WithHttpHealthCheck("/healthz", endpointName: "http")
            .WithEnvironment("IDENTITY_ROLE", "Gateway")
            .WithEnvironment("Identity__ClientId", "identity-admin-gateway")
    .WithEnvironment("Identity__MaximumAuthenticationAgeSeconds", "10")
            .WithEnvironment("Identity__ServerUrl", keycloak.GetEndpoint("http"))
            .WithEnvironment("Identity__ClientSecret", secret)
            .WithEnvironment("Identity__ReaderSecret", readerSecret)
            .WithEnvironment("Identity__DownstreamUrl", service.GetEndpoint("grpc"))
            .WithReference(redis)
            .WaitFor(keycloak)
            .WaitFor(service);
        var app = builder.Build();
        await using var disposeApp = app.ConfigureAwait(false);
        await app.RunAsync().ConfigureAwait(false);
    }
}
