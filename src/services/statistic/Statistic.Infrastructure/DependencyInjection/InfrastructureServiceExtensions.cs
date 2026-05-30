// <copyright file="InfrastructureServiceExtensions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SharedKernel.Core.Exceptions;
using SharedKernel.Infrastructure.Messaging;
using Statistic.Application.Statistics;
using Statistic.Infrastructure.Statistics;
using Wolverine;

namespace Statistic.Infrastructure.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring infrastructure services for the Statistic application.
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Adds and configures infrastructure services for the Statistic application.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder instance.</param>
    /// <param name="applicationAssembly">The application assembly to scan for services.</param>
    public static void AddInfrastructureServices(this WebApplicationBuilder builder, Assembly applicationAssembly)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(applicationAssembly);

        builder.Services.AddSingleton<ISnapshotStore, InMemorySnapshotStore>();

        // Real-time dashboard updates are now produced by Wolverine integration event handlers.
        builder.Services.AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy =
                    System.Text.Json.JsonNamingPolicy.CamelCase;
            });
    }

    /// <summary>
    /// Configures Wolverine messaging for consuming Statistic integration events.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder instance.</param>
    /// <param name="options">The Wolverine options.</param>
    public static void ConfigureWolverine(this WebApplicationBuilder builder, WolverineOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        string rabbitConnectionString = builder.Configuration.GetConnectionString("rabbitmq")
            ?? throw new ConfigurationMissingException("RabbitMq");
        string normalizedRabbit = WolverinePersistenceConfigurator.NormalizeRabbitConnectionString(rabbitConnectionString);

        options.Discovery.IncludeAssembly(typeof(DisplayOperationStateChangedHandler).Assembly);
        WolverinePersistenceConfigurator.ConfigureStatelessRuntime(options, builder.Environment.IsDevelopment(), normalizedRabbit);
    }

    /// <summary>
    /// Uses statistic infrastructure middleware.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same <see cref="IApplicationBuilder"/> instance.</returns>
    public static IApplicationBuilder UseInfrastructureServices(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app;
    }
}
