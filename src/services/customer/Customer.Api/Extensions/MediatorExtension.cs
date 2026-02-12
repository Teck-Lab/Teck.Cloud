using System.Reflection;
using Customer.Application;
using SharedKernel.Infrastructure.Behaviors;

namespace Customer.Api.Extensions;

/// <summary>
/// Provides extension methods for configuring Mediator infrastructure in a <see cref="WebApplicationBuilder"/>.
/// </summary>
internal static class MediatorExtension
{
    /// <summary>
    /// Registers Mediator infrastructure, including handler scanning and pipeline behaviors.
    /// Configures Mediator to scan the specified application assembly for handlers and behaviors,
    /// and registers custom pipeline behaviors in the defined order.
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
            // Specify the assembly to scan for Mediator handlers and pipeline behaviors.
            options.Assemblies = [typeof(ICustomerApplication)];
            options.ServiceLifetime = ServiceLifetime.Scoped;

            // Configure the request pipeline by registering behaviors in the desired order.
            // Behaviors are executed in listed order, wrapping around the core handler.
            options.PipelineBehaviors =
            [
                typeof(LoggingBehavior<,>),         // Logs request start, end, and duration.
            ];
        });

        return builder;
    }
}
