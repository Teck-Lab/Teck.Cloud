using System.Reflection;
using JasperFx;
using JasperFx.CodeGeneration;
using SharedKernel.Infrastructure.IdentityEvents;
using Wolverine;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

namespace SharedKernel.Api.TestHost;

/// <summary>A test-only owner of the real identity listener and PostgreSQL durable inbox.</summary>
public static class IdentityEventTestHost
{
    /// <summary>Builds the production adapter with isolated fixture dependencies.</summary>
    /// <param name="database">Owned PostgreSQL connection.</param>
    /// <param name="broker">Owned RabbitMQ connection.</param>
    /// <param name="register">Real directory/cache registration supplied by the fixture.</param>
    /// <param name="handlerMode">Static mode for the repository precompiled-handler verification lane.</param>
    /// <param name="applicationAssembly">The owning test assembly which compiles generated handler sources.</param>
    /// <returns>The unstarted host.</returns>
    public static IHost Build(string database, string broker, Action<IServiceCollection> register, TypeLoadMode handlerMode, Assembly applicationAssembly)
    {
        var builder = Host.CreateApplicationBuilder();
        register(builder.Services);
        _ = builder.UseWolverine(options =>
        {
            options.ApplicationAssembly = applicationAssembly;
            _ = options.Discovery.DisableConventionalDiscovery();
            var output = Environment.GetEnvironmentVariable("TECK_CODEGEN_OUTPUT");
            if (!string.IsNullOrEmpty(output))
            {
                options.CodeGeneration.GeneratedCodeOutputPath = output;
                options.CodeGeneration.SourceCodeWritingEnabled = true;
            }

            options.CodeGeneration.TypeLoadMode = handlerMode;
            _ = options.PersistMessagesWithPostgresql(database, "identity_events");
            _ = options.UseRabbitMq(new Uri(broker)).AutoProvision()
                .DeclareExchange("identity-events", exchange => exchange.BindQueue("identity-events"));
            options.ListenToKeycloakOrganizationEvents("identity-events", "fixture-issuer", "teck-local");
            _ = options.Services.Configure<JasperFxOptions>(configuration =>
            {
                configuration.Production.ResourceAutoCreate = AutoCreate.All;
                configuration.Production.GeneratedCodeMode = handlerMode;
                configuration.Production.SourceCodeWritingEnabled = true;
                configuration.Production.GeneratedCodeOutputPath = output ?? Path.Combine(builder.Environment.ContentRootPath, ".artifacts", "wolverine-generated");
            });
        });
        return builder.Build();
    }
}
