// <copyright file="MediatorExtension.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Order.Application;
using SharedKernel.Infrastructure.Behaviors;

namespace Order.Api.Extensions;

/// <summary>
/// Mediator registration helpers for Order API.
/// </summary>
internal static class MediatorExtension
{
    /// <summary>
    /// Registers mediator handlers and pipeline behaviors.
    /// </summary>
    /// <param name="builder">Web application builder.</param>
    /// <param name="applicationAssembly">Application assembly.</param>
    /// <returns>The web application builder.</returns>
    public static WebApplicationBuilder AddMediatorInfrastructure(
        this WebApplicationBuilder builder,
        Assembly applicationAssembly)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(applicationAssembly);

        builder.Services.AddMediator(static options =>
        {
            options.Assemblies = [typeof(IOrderApplication)];
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.PipelineBehaviors =
            [
                typeof(LoggingBehavior<,>),
            ];
        });

        return builder;
    }
}
