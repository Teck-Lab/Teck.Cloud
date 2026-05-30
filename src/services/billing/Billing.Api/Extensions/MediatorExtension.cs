// <copyright file="MediatorExtension.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Billing.Application;
using SharedKernel.Infrastructure.Behaviors;

namespace Billing.Api.Extensions;

/// <summary>
/// Provides extension methods for configuring Mediator infrastructure in a <see cref="WebApplicationBuilder"/>.
/// </summary>
internal static class MediatorExtension
{
    /// <summary>
    /// Registers Mediator infrastructure, including handler scanning and pipeline behaviors.
    /// </summary>
    /// <param name="builder">The <see cref="WebApplicationBuilder"/> used to configure services.</param>
    /// <param name="applicationAssembly">The assembly containing the application-specific Mediator handlers and behaviors.</param>
    /// <returns>The same <see cref="WebApplicationBuilder"/> instance for chaining.</returns>
    public static WebApplicationBuilder AddMediatorInfrastructure(
        this WebApplicationBuilder builder,
        Assembly applicationAssembly)
    {
        builder.Services.AddMediator((Mediator.MediatorOptions options) =>
        {
            options.Assemblies = [typeof(IBillingApplication)];
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.PipelineBehaviors =
            [
                typeof(LoggingBehavior<,>),
            ];
        });

        return builder;
    }
}
