// <copyright file="TestLocationApiHost.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Location.Api.Endpoints.V1.Service;
using Location.Application;
using Location.Infrastructure.DependencyInjection;
using Location.Infrastructure.Persistence;
using Location.Infrastructure.Service;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Persistence.Database.EFCore.Interceptors;
using Testcontainers.PostgreSql;

namespace Location.IntegrationTests.TestSupport;

internal sealed class TestLocationApiHost : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly PostgreSqlContainer dbContainer;

    private TestLocationApiHost(WebApplication app, HttpClient client, string localStorageDirectory, PostgreSqlContainer dbContainer)
    {
        this.app = app;
        this.Client = client;
        this.LocalStorageDirectory = localStorageDirectory;
        this.dbContainer = dbContainer;
    }

    public HttpClient Client { get; }

    public string LocalStorageDirectory { get; }

    public static async Task<TestLocationApiHost> StartAsync()
    {
        PostgreSqlContainer dbContainer = new PostgreSqlBuilder("postgres:latest")
            .WithDatabase("location_testdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await dbContainer.StartAsync(TestContext.Current.CancellationToken);

        string dbConnectionString = dbContainer.GetConnectionString();

        var migrationOptions = new DbContextOptionsBuilder<TemplateFontMetadataDbContext>()
            .UseNpgsql(dbConnectionString, npgsql => npgsql.MigrationsAssembly("Teck.Cloud.Migrations.PostgreSQL"))
            .Options;
        await using (var migrationContext = new TemplateFontMetadataDbContext(migrationOptions, null))
        {
            await migrationContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        string localStorageDirectory = Path.Combine(
            Path.GetTempPath(),
            "teck-cloud",
            "location-font-integration-tests",
            Guid.NewGuid().ToString("N"));

        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
        });

        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddOutputCache();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:db-write"] = dbConnectionString,
            ["Database:Provider"] = "postgres",
            [$"{TemplateFontStorageOptions.Section}:LocalDirectory"] = localStorageDirectory,
            [$"{TemplateFontStorageOptions.Section}:ObjectKeyTemplate"] = "tenant-fonts/{tenantId}/{fontKey}",
            [$"{TemplateFontStorageOptions.Section}:MaxFontBytes"] = (1024 * 1024).ToString(),
        });

        builder.Services.Configure<TemplateFontStorageOptions>(builder.Configuration.GetSection(TemplateFontStorageOptions.Section));
        builder.Services.AddSingleton(Substitute.For<ISender>());
        builder.Services.AddSingleton<AuditingInterceptor>();
        builder.Services.AddSingleton<SoftDeleteInterceptor>();
        builder.AddInfrastructureServices(typeof(ILocationApplication).Assembly);

        // Override EF Core's scoped factory with a custom singleton that creates options directly
        // to avoid FastEndpoints startup resolving scoped IDbContextOptionsConfiguration from root
        builder.Services.AddSingleton<IDbContextFactory<TemplateFontMetadataDbContext>>(sp =>
        {
            var optionsBuilder = new DbContextOptionsBuilder<TemplateFontMetadataDbContext>();
            optionsBuilder.UseNpgsql(dbConnectionString);
            return new TestDbContextFactory<TemplateFontMetadataDbContext>(optionsBuilder.Options);
        });

        builder.Services.AddFastEndpointsInfrastructure(typeof(ILocationApplication).Assembly, typeof(UploadTemplateFontEndpoint).Assembly);

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<TestAuthHandlerOptions, TestAuthHandler>(TestAuthHandler.SchemeName, static _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, TestAuthorizationPolicyProvider>();

        WebApplication app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseInfrastructureServices();
        app.UseFastEndpointsInfrastructure("location");

        await app.StartAsync(TestContext.Current.CancellationToken);
        return new TestLocationApiHost(app, app.GetTestClient(), localStorageDirectory, dbContainer);
    }

    public async ValueTask DisposeAsync()
    {
        this.Client.Dispose();
        await this.app.DisposeAsync();
        await this.dbContainer.DisposeAsync();

        if (Directory.Exists(this.LocalStorageDirectory))
        {
            Directory.Delete(this.LocalStorageDirectory, recursive: true);
        }
    }

    private sealed class TestDbContextFactory<TContext>(DbContextOptions<TContext> options) : IDbContextFactory<TContext>
        where TContext : DbContext
    {
        public TContext CreateDbContext()
        {
            return (TContext)Activator.CreateInstance(typeof(TContext), options, null)!;
        }
    }
}
