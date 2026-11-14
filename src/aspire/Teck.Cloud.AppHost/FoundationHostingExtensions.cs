namespace Teck.Cloud.AppHost;

/// <summary>Local dependency resources shared by development and application E2E.</summary>
internal static class FoundationHostingExtensions
{
    /// <summary>Adds PostgreSQL, RabbitMQ and the development identity provider.</summary>
    /// <param name="builder">The application builder.</param>
    internal static void AddFoundationServices(this IDistributedApplicationBuilder builder)
    {
        _ = builder.AddPostgres("postgres").AddDatabase("application-db");
        _ = builder.AddRabbitMQ("rabbitmq").WithManagementPlugin();
        _ = builder.AddKeycloak("keycloak")
            .WithRealmImport(Path.Combine(builder.AppHostDirectory, "Identity", "Realms"));
    }
}
