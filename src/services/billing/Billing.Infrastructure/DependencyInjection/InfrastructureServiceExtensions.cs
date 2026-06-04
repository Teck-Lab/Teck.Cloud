// <copyright file="InfrastructureServiceExtensions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Billing.Application.Common.Interfaces;
using Billing.Infrastructure.Payment;
using Billing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using SharedKernel.Core.Exceptions;
using SharedKernel.Core.Pricing;
using SharedKernel.Persistence.Database;

namespace Billing.Infrastructure.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring Billing infrastructure services.
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registers Billing infrastructure services into the DI container.
    /// </summary>
    /// <param name="builder">The <see cref="WebApplicationBuilder"/> to configure.</param>
    /// <param name="applicationAssembly">The application assembly to scan for services.</param>
    public static void AddInfrastructureServices(this WebApplicationBuilder builder, Assembly applicationAssembly)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(applicationAssembly);

        DatabaseProvider databaseProvider = builder.Configuration.GetDatabaseProvider();
        string writeConnectionString = builder.Configuration.GetConnectionString("db-write")
            ?? throw new ConfigurationMissingException("Database (write)");
        string readConnectionString = builder.Configuration.GetConnectionString("db-read")
            ?? writeConnectionString;

        builder.AddCustomDbContexts<BillingDbContext, BillingReadDbContext>(
            assembly: typeof(BillingDbContext).Assembly,
            defaultWriteConnectionString: writeConnectionString,
            defaultReadConnectionString: readConnectionString,
            provider: databaseProvider);

        builder.Services.AddBillingInfrastructureRepositories();
        builder.Services.AddScoped<IPaymentGateway, StripePaymentGateway>();
    }

    /// <summary>
    /// Configures the Billing infrastructure middleware pipeline.
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder"/> to configure.</param>
    public static void UseInfrastructureServices(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
    }
}
