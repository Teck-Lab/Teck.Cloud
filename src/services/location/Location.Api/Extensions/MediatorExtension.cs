// <copyright file="MediatorExtension.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Location.Application;
using SharedKernel.Infrastructure.Behaviors;

namespace Location.Api.Extensions;

internal static class MediatorExtension
{
    public static WebApplicationBuilder AddMediatorInfrastructure(
        this WebApplicationBuilder builder,
        Assembly applicationAssembly)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(applicationAssembly);

        builder.Services.AddMediator(static options =>
        {
            options.Assemblies = [typeof(ILocationApplication)];
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.PipelineBehaviors =
            [
                typeof(LoggingBehavior<,>),
                typeof(LicenseEnforcementBehavior<,>),
            ];
        });

        return builder;
    }
}
