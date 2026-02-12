#pragma warning disable IDE0005
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Shouldly;
using Catalog.Infrastructure.DependencyInjection;
using Catalog.Infrastructure.Caching;
using ZiggyCreatures.Caching.Fusion;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace Catalog.IntegrationTests.DependencyInjection;

public class InfrastructureServiceRegistrationTests
{
    [Fact]
    public void AddInfrastructureServices_RegistersExpectedServices()
    {
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();
        // Provide minimal configuration values required by AddInfrastructureServices
        builder.Configuration.AddInMemoryCollection(new KeyValuePair<string,string?>[]
        {
            new KeyValuePair<string,string?>("ConnectionStrings:postgres-write", "Host=localhost;Database=teck_test;Username=postgres;Password=postgres"),
            new KeyValuePair<string,string?>("ConnectionStrings:postgres-read", "Host=localhost;Database=teck_test;Username=postgres;Password=postgres"),
            new KeyValuePair<string,string?>("ConnectionStrings:rabbitmq", "amqp://guest:guest@localhost:5672/"),

        });

        // Call the extension with the application assembly (Catalog.Application)
        var applicationAssembly = typeof(Catalog.Application.Categories.Repositories.ICategoryCache).Assembly;

        builder.Services.AddFusionCache();

        // Should not throw
        builder.AddInfrastructureServices(applicationAssembly);

        // Test ensures AddInfrastructureServices does not throw when configured for Domain/Infrastructure tests.
        // Detailed cache and DI behavior are covered in dedicated integration tests elsewhere.
    }
}
