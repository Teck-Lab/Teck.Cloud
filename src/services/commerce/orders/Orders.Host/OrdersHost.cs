using System.Net;
#if !COMMERCE_WOLVERINE_CODEGEN
using System.Reflection;
#endif
using JasperFx;
using JasperFx.CodeGeneration;
using Keycloak.AuthServices.Sdk.Kiota;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Infrastructure.Auth;
using SharedKernel.Infrastructure.Hosting;
using SharedKernel.Infrastructure.Messaging;
using SharedKernel.Infrastructure.MultiTenancy;
using SharedKernel.Persistence.Database;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Orders.Application;
using Teck.Cloud.Commerce.Orders.Application.Orders.Settings;
using Teck.Cloud.Commerce.Orders.Host.Composition;
using Teck.Cloud.Commerce.Orders.Host.Grpc;
using Teck.Cloud.Commerce.Orders.Host.Workers;
using Teck.Cloud.Commerce.Orders.Infrastructure;
using Teck.Cloud.Commerce.Orders.Infrastructure.Clients.Placement;
using Teck.Cloud.Commerce.Orders.Infrastructure.Clients.Stripe;
using Teck.Cloud.Commerce.Orders.Infrastructure.Database;
using Wolverine;
using Wolverine.RabbitMQ;
#if !COMMERCE_WOLVERINE_CODEGEN
using Wolverine.Runtime.Handlers;
#endif

namespace Teck.Cloud.Commerce.Orders.Host;

/// <summary>Host composition for the Orders service.</summary>
public static class OrdersHost
{
#if !COMMERCE_WOLVERINE_CODEGEN
    private const string GeneratedRegistryDiagnosticPrefix = "Commerce static Wolverine registry invalid:";
#endif

    /// <summary>Builds the service host without starting it.</summary>
    /// <param name="args">Process arguments.</param>
    /// <returns>The composed application.</returns>
    /// <exception cref="InvalidOperationException">A required host setting is missing or unsafe.</exception>
    public static WebApplication Build(string[] args) => Build(args, configureServices: null);

    /// <summary>Builds the service host with an isolated test hook for replacing foundational services.</summary>
    /// <param name="args">Process arguments.</param>
    /// <param name="configureServices">An optional final registration callback used by isolated tests.</param>
    /// <returns>The composed application.</returns>
    /// <exception cref="InvalidOperationException">A required host setting is missing or unsafe.</exception>
    public static WebApplication Build(string[] args, Action<IServiceCollection>? configureServices)
    {
        var builder = WebApplication.CreateBuilder(args);
        var configuration = builder.Configuration;
        var section = configuration.GetSection("CommerceOrders");
        var identity = ReadIdentity(section);
        ConfigureListeners(builder, section);
        AddHostServices(builder, configuration, section, identity);
        configureServices?.Invoke(builder.Services);
        var app = builder.Build();
        ConfigurePipeline(app);
        return app;
    }

    /// <summary>Applies service-owned migrations and exits without starting the host.</summary>
    /// <param name="args">Process arguments.</param>
    /// <returns>The migration task.</returns>
    public static async Task MigrateAsync(string[] args)
    {
        var app = Build(args);
        await using (app.ConfigureAwait(false))
        {
            await ApplyMigrationsAsync(app.Services, app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        }
    }

    private static (OrdersIdentityOptions Identity, Uri ServerUrl, IdentityTransport Transport) ReadIdentity(IConfigurationSection section)
    {
        var identity = section.GetSection("Identity").Get<OrdersIdentityOptions>()
            ?? throw new InvalidOperationException("CommerceOrders:Identity is required.");
        var transport = Enum.TryParse<IdentityTransport>(identity.Transport, ignoreCase: true, out var parsed)
            ? parsed
            : throw new InvalidOperationException("CommerceOrders:Identity:Transport must be Https, AmbientMesh or IsolatedDevelopment.");
        return Uri.TryCreate(identity.ServerUrl, UriKind.Absolute, out var serverUrl)
            && !string.IsNullOrWhiteSpace(identity.Realm)
            && !string.IsNullOrWhiteSpace(identity.TenantReaderClientSecret)
            ? (identity, serverUrl, transport)
            : throw new InvalidOperationException("CommerceOrders:Identity requires ServerUrl, Realm and TenantReaderClientSecret.");
    }

    private static void ConfigureListeners(WebApplicationBuilder builder, IConfigurationSection section)
    {
        var listen = section.GetSection("Listen").Get<OrdersListenOptions>() ?? new OrdersListenOptions();
        var grpc = ParseListen(listen.GrpcUrl, "CommerceOrders:Listen:GrpcUrl");
        var http = ParseListen(listen.HttpUrl, "CommerceOrders:Listen:HttpUrl");
        if (grpc.Port == http.Port)
        {
            throw new InvalidOperationException("gRPC and HTTP endpoints require distinct ports for cleartext HTTP/2.");
        }

        _ = builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(Address(grpc), grpc.Port, endpoint => endpoint.Protocols = HttpProtocols.Http2);
            options.Listen(Address(http), http.Port, endpoint => endpoint.Protocols = HttpProtocols.Http1);
        });
    }

    private static void AddHostServices(
        WebApplicationBuilder builder,
        IConfiguration configuration,
        IConfigurationSection section,
        (OrdersIdentityOptions Identity, Uri ServerUrl, IdentityTransport Transport) identity)
    {
        builder.AddFoundationHost();
        _ = builder.Services.Configure<ForwardedHeadersOptions>(options =>
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        _ = builder.Services.AddPersistenceFoundation();
        AddOrdersServices(builder, configuration, section);
        AddIdentityServices(builder, identity);
        AddMessaging(builder, configuration, section);
    }

    private static void AddOrdersServices(WebApplicationBuilder builder, IConfiguration configuration, IConfigurationSection section)
    {
        _ = builder.Services.AddOrdersApplication();
        _ = builder.Services.AddOrdersInfrastructure(configuration);
        _ = builder.Services.Configure<CatalogOptions>(section.GetSection("Catalog"));
        _ = builder.Services.Configure<PlacementProbeOptions>(section.GetSection("Probe"));
        _ = builder.Services.Configure<ProviderWorkOptions>(section.GetSection("ProviderWork"));
        _ = builder.Services.Configure<OrderCursorOptions>(section.GetSection("Cursor"));
        _ = builder.Services.AddCommerceStripe(configuration);
        _ = builder.Services.AddCommercePlacement(configuration);
        _ = builder.Services.AddFusionCache();
        _ = builder.Services.AddGrpc();
        _ = builder.Services.AddHostedService<PlacementProbeWorker>();
        _ = builder.Services.AddHostedService<ProviderReconciliationWorker>();
    }

    private static void AddIdentityServices(
        WebApplicationBuilder builder,
        (OrdersIdentityOptions Identity, Uri ServerUrl, IdentityTransport Transport) identity)
    {
        var options = identity.Identity;
        _ = builder.Services.AddFoundationKeycloakAdmin(
            new KeycloakAdminClientOptions
            {
                AuthServerUrl = identity.ServerUrl.AbsoluteUri,
                Realm = options.Realm,
                Resource = options.TenantReaderClientId,
                Credentials = new() { Secret = options.TenantReaderClientSecret },
            },
            identity.Transport);
        _ = builder.Services.AddKeycloakTenantStore();
        _ = builder.Services.AddCommerceOrdersServiceAuth(new CommerceAuthOptions
        {
            ServerUrl = identity.ServerUrl,
            Realm = options.Realm,
            PublicIssuer = string.IsNullOrWhiteSpace(options.PublicIssuer) ? null : new Uri(options.PublicIssuer, UriKind.Absolute),
            Transport = identity.Transport,
        });
    }

    private static void AddMessaging(WebApplicationBuilder builder, IConfiguration configuration, IConfigurationSection section)
    {
#if !COMMERCE_WOLVERINE_CODEGEN
        RequireGeneratedHandlerRegistry();
#endif
        var writeConnection = configuration.GetConnectionString("orders")
            ?? throw new InvalidOperationException("ConnectionStrings:orders is required.");
        var rabbit = configuration.GetConnectionString("rabbitmq")
            ?? throw new InvalidOperationException("ConnectionStrings:rabbitmq is required.");
        var messagingSchema = section["MessagingSchema"] ?? "orders_messaging";
        _ = builder.Host.UseWolverine(options =>
        {
            options.ApplicationAssembly = typeof(OrdersHost).Assembly;
            options.UseFoundationMessaging<OrdersWriteDbContext>(writeConnection, messagingSchema);
            _ = options.CodeGeneration.AlwaysUseServiceLocationFor<ITenantDirectory>();
            options.UseRequiredRabbitMq(new Uri(rabbit, UriKind.Absolute));
            options.RouteIntegrationEvent<LicenseDesiredEvent>(CommerceMessaging.LicenseDesired);
            _ = options.ListenToRabbitQueue(CommerceMessaging.LicenseState);
            _ = options.Discovery.IncludeAssembly(typeof(Teck.Cloud.Commerce.Orders.Application.Orders.EventHandlers.IntegrationEvents.LicenseStateHandler).Assembly);
            _ = options.Discovery.IncludeAssembly(typeof(LicenseDesiredEvent).Assembly);
            ConfigureCodeGeneration(options.CodeGeneration);
            _ = options.Services.Configure<JasperFxOptions>(jasperFx =>
            {
                ConfigureCodeGenerationProfile(jasperFx.Development);
                ConfigureCodeGenerationProfile(jasperFx.Production);
            });
        });
    }

    private static void ConfigureCodeGeneration(GenerationRules rules)
    {
#if COMMERCE_WOLVERINE_CODEGEN
        rules.TypeLoadMode = TypeLoadMode.Dynamic;
        rules.SourceCodeWritingEnabled = true;
        rules.GeneratedCodeOutputPath = "Internal/Generated";
#else
        rules.TypeLoadMode = TypeLoadMode.Static;
        rules.SourceCodeWritingEnabled = false;
#endif
    }

    private static void ConfigureCodeGenerationProfile(Profile profile)
    {
#if COMMERCE_WOLVERINE_CODEGEN
        profile.GeneratedCodeMode = TypeLoadMode.Dynamic;
        profile.GeneratedCodeOutputPath = "Internal/Generated";
        profile.AssertAllPreGeneratedTypesExist = false;
#else
        profile.GeneratedCodeMode = TypeLoadMode.Static;
        profile.AssertAllPreGeneratedTypesExist = true;
#endif
    }

#if !COMMERCE_WOLVERINE_CODEGEN
    /// <summary>Fails a normal build before Wolverine unless the committed generated registry is loadable.</summary>
    /// <exception cref="InvalidOperationException">The generated registry is missing, ambiguous or malformed.</exception>
    private static void RequireGeneratedHandlerRegistry()
    {
        var assembly = typeof(OrdersHost).Assembly;
        var assemblyName = assembly.GetName().Name ?? nameof(OrdersHost);
        string? problem;
        try
        {
            problem = FindGeneratedRegistryProblem(assembly);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(GeneratedRegistryDiagnosticPrefix + " " + assemblyName, exception);
        }

        if (problem is not null)
        {
            throw new InvalidOperationException(GeneratedRegistryDiagnosticPrefix + " " + assemblyName + ": " + problem);
        }
    }

    private static string? FindGeneratedRegistryProblem(Assembly assembly)
    {
        var candidates = assembly.ExportedTypes
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(HandlerRegistry).IsAssignableFrom(type))
            .ToArray();
        if (candidates.Length == 0)
        {
            return "no generated HandlerRegistry type is exported";
        }

        if (candidates.Length > 1)
        {
            return "more than one generated HandlerRegistry type is exported";
        }

        var registryType = candidates[0];
        if (!string.Equals(registryType.Name, HandlerRegistry.GeneratedTypeName, StringComparison.Ordinal))
        {
            return "the exported HandlerRegistry type is not named " + HandlerRegistry.GeneratedTypeName;
        }

        var constructor = registryType.GetConstructor(Type.EmptyTypes);
        return constructor?.Invoke(parameters: null) switch
        {
            null => "the generated HandlerRegistry type has no public parameterless constructor",
            HandlerRegistry registry when registry.HandlerTypes() is not { Length: > 0 } => "the generated HandlerRegistry declares no handler types",
            HandlerRegistry registry when registry.MessageTypes() is null => "the generated HandlerRegistry returns null message types",
            _ => null,
        };
    }
#endif

    private static void ConfigurePipeline(WebApplication app)
    {
        _ = app.UseForwardedHeaders();
        _ = app.UseRouting();
        _ = app.UseAuthentication();
        _ = app.UseAuthorization();
        _ = app.UseMiddleware<OperationScopeMiddleware>();
        app.MapFoundationHealth();
        _ = app.MapGrpcService<OrdersGrpcService>();
        app.MapOrdersWebhook();
    }

    private static async Task ApplyMigrationsAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var scope = services.CreateAsyncScope();
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<OrdersWriteDbContext>();
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await scope.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static Uri ParseListen(string value, string key) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
            ? uri
            : throw new InvalidOperationException(key + " must be an absolute cleartext HTTP URL; terminate TLS at ingress.");

    private static IPAddress Address(Uri uri) =>
        IPAddress.TryParse(uri.Host, out var address) ? address : IPAddress.Any;
}
