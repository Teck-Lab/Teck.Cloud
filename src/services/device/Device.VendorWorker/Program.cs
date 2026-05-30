// <copyright file="Program.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Device.VendorWorker.Vendors;
using SharedKernel.Infrastructure.Messaging;
using Wolverine;

namespace Device.VendorWorker;

/// <summary>
/// Entry point for the Device vendor ESL dispatch worker.
/// </summary>
internal static class Program
{
    private static async Task Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.AddServiceDefaults();

        // Register vendor adapters. Each adapter advertises its provider key; the rendered-integration
        // handler filters incoming messages by EslProvider so only the matching adapter dispatches.
        builder.Services.AddSingleton<IEslDeviceServer, StubEslDeviceServer>();
        builder.Services.AddSingleton<IEslDeviceServer, HanshowEslDeviceServer>();

        string? rabbitConnectionString = builder.Configuration.GetConnectionString("rabbitmq");
        if (string.IsNullOrWhiteSpace(rabbitConnectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'rabbitmq' is required for Device.VendorWorker.");
        }

        string normalizedRabbit = WolverinePersistenceConfigurator.NormalizeRabbitConnectionString(rabbitConnectionString);
        bool isDevelopment = builder.Environment.IsDevelopment();
        Assembly workerAssembly = typeof(Program).Assembly;

        builder.UseWolverine(options =>
        {
            options.Discovery.IncludeAssembly(workerAssembly);
            WolverinePersistenceConfigurator.ConfigureStatelessRuntime(options, isDevelopment, normalizedRabbit);
        });

        IHost app = builder.Build();
        await app.RunAsync().ConfigureAwait(false);
    }
}
