using System.Text.Json;

namespace Teck.Cloud.AppHost;

/// <summary>Local dependency resources shared by development and application E2E.</summary>
internal static class FoundationHostingExtensions
{
    /// <summary>Adds PostgreSQL, RabbitMQ and the development identity provider.</summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="identityDirectory">Optional shared identity assets directory for isolated fixture AppHosts.</param>
    /// <exception cref="InvalidOperationException">The shared identity image or build definition is missing.</exception>
    internal static void AddFoundationServices(this IDistributedApplicationBuilder builder, string? identityDirectory = null)
    {
        identityDirectory ??= Path.Combine(builder.AppHostDirectory, "Identity");
        var postgres = builder.AddPostgres("postgres");
        _ = postgres.AddDatabase("application-db");
        var identityDatabase = postgres.AddDatabase("keycloak-db", "keycloak");
        _ = builder.AddRabbitMQ("rabbitmq").WithManagementPlugin();
        var customerSecret = builder.AddParameter("keycloak-customer-gateway-secret", new GenerateParameterDefault { MinLength = 48, Special = false }, secret: true);
        var readerSecret = builder.AddParameter("keycloak-tenant-reader-secret", new GenerateParameterDefault { MinLength = 48, Special = false }, secret: true);
        var adminSecret = builder.AddParameter("keycloak-admin-gateway-secret", new GenerateParameterDefault { MinLength = 48, Special = false }, secret: true);
        using var runtime = JsonDocument.Parse(File.ReadAllText(Path.Combine(identityDirectory, "runtime.json")));
        var image = runtime.RootElement.GetProperty("image").GetString() ?? throw new InvalidOperationException("The Keycloak image is required.");
        var dockerfile = runtime.RootElement.GetProperty("dockerfile").GetString() ?? throw new InvalidOperationException("The Keycloak Dockerfile is required.");
        var databaseEndpoint = postgres.GetEndpoint("tcp");
        var keycloak = builder.AddKeycloak("keycloak")
            .WithEnvironment("TECK_TENANT_READER_SECRET", readerSecret)
            .WithEnvironment("TECK_CUSTOMER_GATEWAY_SECRET", customerSecret)
            .WithEnvironment("TECK_ADMIN_GATEWAY_SECRET", adminSecret)
            .WithEnvironment("KC_DB", "postgres")
            .WithEnvironment("KC_DB_URL", ReferenceExpression.Create($"jdbc:postgresql://{databaseEndpoint.Property(EndpointProperty.Host)}:{databaseEndpoint.Property(EndpointProperty.Port)}/keycloak"))
            .WithEnvironment("KC_DB_USERNAME", postgres.Resource.UserNameReference)
            .WithEnvironment("KC_DB_PASSWORD", postgres.Resource.PasswordParameter)
            .WithEnvironment("KC_HTTP_ENABLED", "true")
            .WithEnvironment("KC_HOSTNAME_STRICT", "false")
            .WithRealmImport(Path.Combine(identityDirectory, "Realms"))
            .WithDockerfile(Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "..")), dockerfile)
            .WaitFor(identityDatabase);
        _ = keycloak.WithAnnotation(new ContainerImageAnnotation { Image = image }, ResourceAnnotationMutationBehavior.Replace);
        if (runtime.RootElement.GetProperty("optimized").GetBoolean())
        {
            _ = keycloak.WithArgs(context =>
            {
                context.Args.Clear();
                context.Args.Add("start");
                context.Args.Add("--optimized");
                context.Args.Add("--import-realm");
            });
        }
    }
}
