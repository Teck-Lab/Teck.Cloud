using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SharedKernel.Infrastructure.Hosting;

/// <summary>Registers typed settings with a service-owned source-generated validator.</summary>
public static class OptionsExtensions
{
    /// <summary>Validates configured settings during host startup without reflection-based validation.</summary>
    /// <typeparam name="TOptions">The service settings.</typeparam>
    /// <typeparam name="TValidator">A validator, typically emitted by OptionsValidatorAttribute.</typeparam>
    /// <param name="services">Host registrations.</param>
    /// <param name="configure">Typed configuration assignment or source-generated binding.</param>
    /// <returns>The configured registrations.</returns>
    public static IServiceCollection AddValidatedSettings<TOptions, TValidator>(this IServiceCollection services, Action<TOptions> configure)
        where TOptions : class
        where TValidator : class, IValidateOptions<TOptions>
    {
        _ = services.AddSingleton<IValidateOptions<TOptions>, TValidator>();
        _ = services.AddOptions<TOptions>().Configure(configure).ValidateOnStart();
        return services;
    }
}
