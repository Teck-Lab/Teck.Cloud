using JasperFx;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Infrastructure.Auth;
using SharedKernel.Infrastructure.Hosting;
using SharedKernel.Infrastructure.Messaging;
using SharedKernel.Infrastructure.MultiTenancy;
using SharedKernel.Persistence.Caching;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Licensing.Application.Entitlements.Features.GetCurrentLicense.V1;
using Teck.Cloud.Commerce.Licensing.Application.Entitlements.Ports;
using Teck.Cloud.Commerce.Licensing.Infrastructure;
using Teck.Cloud.Commerce.Licensing.Infrastructure.Database;
using Teck.Cloud.Commerce.Licensing.Infrastructure.Security;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.RabbitMQ;
using OrdersV1 = Teck.Cloud.Commerce.Orders.V1;

namespace Teck.Cloud.Commerce.Licensing.Host;

/// <summary>Composes the separate licensing process without opening a database during Build.</summary>
public static class LicensingHost
{
    private const string MessagingSchema = "licensing_messages";

    /// <summary>Builds the licensing web application from trusted configuration.</summary>
    /// <param name="args">Host configuration arguments.</param>
    /// <returns>The composed application; nothing is migrated or started.</returns>
    public static WebApplication Build(string[] args) => Build(args, configureServices: null);

    /// <summary>Builds the licensing web application with an optional test-only service override.</summary>
    /// <param name="args">Host configuration arguments.</param>
    /// <param name="configureServices">Optional final registrations; never a production feature switch.</param>
    /// <returns>The composed application; nothing is migrated or started.</returns>
    /// <exception cref="InvalidOperationException">A required host setting is missing or the tenant directory is not registered.</exception>
    public static WebApplication Build(string[] args, Action<IServiceCollection>? configureServices)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddFoundationHost();
        var connections = ReadConnections(builder.Configuration);
        var runtime = ReadRuntimeOptions(builder.Configuration);
        ConfigureTransport(builder, runtime.CleartextHttp2);
        AddHostServices(builder, connections, runtime);
        AddMessaging(builder, connections, runtime);
        configureServices?.Invoke(builder.Services);
        var app = builder.Build();
        ConfigurePipeline(app);
        return app;
    }

    private static LicensingConnections ReadConnections(ConfigurationManager configuration)
    {
        var writeConnection = configuration.GetConnectionString("LicensingWrite")
            ?? throw new InvalidOperationException("The LicensingWrite connection string is required.");
        var readConnection = configuration.GetConnectionString("LicensingRead") ?? writeConnection;
        var broker = configuration.GetConnectionString("rabbitmq")
            ?? configuration["RabbitMq:Uri"]
            ?? throw new InvalidOperationException("A RabbitMQ connection is required.");
        var ordersAddress = configuration["CommerceOrders:GrpcAddress"]
            ?? throw new InvalidOperationException("CommerceOrders:GrpcAddress is required.");
        var redis = configuration.GetConnectionString("redis")
            ?? throw new InvalidOperationException("The redis connection string is required for tenant directory resolution.");
        var tenantReaderSecret = configuration["Commerce:Identity:TenantReaderSecret"]
            ?? throw new InvalidOperationException("Commerce:Identity:TenantReaderSecret is required.");
        return new LicensingConnections(writeConnection, readConnection, broker, ordersAddress, redis, tenantReaderSecret);
    }

    private static LicensingRuntimeOptions ReadRuntimeOptions(ConfigurationManager configuration)
    {
        var standaloneRedis = configuration.GetValue("CommerceLicensing:Redis:Standalone", defaultValue: false);
        var keyRing = LicensingKeyRingOptions.FromConfiguration(configuration.GetSection(LicensingKeyRingOptions.SectionName));
        var auth = CommerceAuth.FromConfiguration(configuration);
        var cleartextHttp2 = configuration.GetValue("CommerceLicensing:Grpc:CleartextHttp2", defaultValue: false);
        var autoCreateMessaging = configuration.GetValue("CommerceLicensing:Messaging:AutoCreate", defaultValue: false);
        var autoMigrate = configuration.GetValue("CommerceLicensing:Database:AutoMigrate", defaultValue: false);
        return new LicensingRuntimeOptions(standaloneRedis, keyRing, auth, cleartextHttp2, autoCreateMessaging, autoMigrate);
    }

    private static void ConfigureTransport(WebApplicationBuilder builder, bool cleartextHttp2)
    {
        if (builder.Configuration["CommerceLicensing:Grpc:ListenUrl"] is { Length: > 0 } listenUrl)
        {
            _ = builder.WebHost.UseUrls(listenUrl);
        }

        _ = builder.WebHost.ConfigureKestrel(options => options.ConfigureEndpointDefaults(endpoint =>
            endpoint.Protocols = cleartextHttp2 ? HttpProtocols.Http2 : HttpProtocols.Http1AndHttp2));
    }

    private static void AddHostServices(
        WebApplicationBuilder builder,
        LicensingConnections connections,
        LicensingRuntimeOptions runtime)
    {
        _ = builder.Services.AddLicensingInfrastructure(runtime.KeyRing);
        _ = runtime.StandaloneRedis
            ? builder.Services.AddFoundationStandaloneRedis(connections.Redis)
            : builder.Services.AddFoundationRedis(connections.Redis);

        _ = builder.Services.AddFoundationKeycloakAdmin(
            new Keycloak.AuthServices.Sdk.Kiota.KeycloakAdminClientOptions
            {
                AuthServerUrl = runtime.Auth.ServerUrl.ToString(),
                Realm = runtime.Auth.Realm,
                Resource = "tenant-reader",
                Credentials = new() { Secret = connections.TenantReaderSecret },
            },
            runtime.Auth.Transport);
        _ = builder.Services.AddKeycloakTenantStore();
        _ = builder.Services.AddDbContext<LicensingReadDbContext>(options => options.UseNpgsql(connections.Read));
        _ = builder.Services.AddDbContextWithWolverineIntegration<LicensingWriteDbContext>(
            options => options.UseNpgsql(connections.Write, provider => provider.MigrationsAssembly(typeof(LicensingWriteDbContext).Assembly.GetName().Name)),
            MessagingSchema);
        _ = builder.Services.AddCommerceLicensingServiceAuth(runtime.Auth);
        _ = builder.Services.AddGrpcClient<OrdersV1.Orders.OrdersClient>(options => options.Address = new Uri(connections.OrdersAddress))
            .AddCommerceOperationCredentials(CommerceAuth.OrdersAudience, runtime.Auth.Transport);

        if (runtime.AutoMigrate)
        {
            _ = builder.Services.AddHostedService<LicensingDatabaseMigrator>();
        }
    }

    private static void AddMessaging(
        WebApplicationBuilder builder,
        LicensingConnections connections,
        LicensingRuntimeOptions runtime)
    {
        _ = builder.UseWolverine(options =>
        {
            options.ApplicationAssembly = typeof(GetCurrentLicenseQuery).Assembly;
            _ = options.Discovery.IncludeAssembly(typeof(GetCurrentLicenseQuery).Assembly);
            _ = options.Discovery.IncludeAssembly(typeof(LicensingHost).Assembly);
            options.UseFoundationMessaging<LicensingWriteDbContext>(connections.Write, MessagingSchema);
            options.UseRequiredRabbitMq(new Uri(connections.Broker));
            options.RouteIntegrationEvent<LicenseStateEvent>(CommerceMessaging.LicenseState);
            _ = options.ListenToRabbitQueue(CommerceMessaging.LicenseDesired);
            _ = options.UseRabbitMq(new Uri(connections.Broker)).AutoProvision()
                .DeclareExchange(CommerceMessaging.LicenseDesired, exchange => exchange.BindQueue(CommerceMessaging.LicenseDesired));
            _ = options.OnException<DbUpdateConcurrencyException>()
                .RetryWithCooldown(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1));
            _ = options.OnException<DbUpdateException>()
                .RetryWithCooldown(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1));
            if (runtime.AutoCreateMessaging)
            {
                _ = options.Services.Configure<JasperFxOptions>(configuration => configuration.Production.ResourceAutoCreate = AutoCreate.All);
            }
        });
    }

    private static void ConfigurePipeline(WebApplication app)
    {
        // A malformed or missing active signing key must stop process startup.
        _ = app.Services.GetRequiredService<ILicenseDocumentService>();
        if (!app.Services.GetRequiredService<IServiceProviderIsService>().IsService(typeof(ITenantDirectory)))
        {
            throw new InvalidOperationException("Commerce licensing requires a registered ITenantDirectory.");
        }

        _ = app.UseRouting();
        _ = app.UseAuthentication();
        _ = app.UseAuthorization();
        _ = app.MapGrpcService<LicensingGrpcService>();
        app.MapFoundationHealth();
    }

    private sealed record LicensingConnections(
        string Write,
        string Read,
        string Broker,
        string OrdersAddress,
        string Redis,
        string TenantReaderSecret);

    private sealed record LicensingRuntimeOptions(
        bool StandaloneRedis,
        LicensingKeyRingOptions KeyRing,
        CommerceAuthOptions Auth,
        bool CleartextHttp2,
        bool AutoCreateMessaging,
        bool AutoMigrate);
}
