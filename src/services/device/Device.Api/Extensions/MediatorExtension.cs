// <copyright file="MediatorExtension.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Device.Application;
using SharedKernel.Infrastructure.Behaviors;

namespace Device.Api.Extensions;

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
            options.Assemblies = [typeof(IDeviceApplication)];
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.PipelineBehaviors =
            [
                typeof(LoggingBehavior<,>),
            ];
        });

        return builder;
    }
}
