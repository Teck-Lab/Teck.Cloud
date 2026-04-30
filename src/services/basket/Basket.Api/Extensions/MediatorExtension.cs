// <copyright file="MediatorExtension.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Basket.Application;
using SharedKernel.Infrastructure.Behaviors;

namespace Basket.Api.Extensions;

/// <summary>
/// Mediator registration helpers for Basket API.
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
            options.Assemblies = [typeof(IBasketApplication)];
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.PipelineBehaviors =
            [
                typeof(LoggingBehavior<,>),
            ];
        });

        return builder;
    }
}
