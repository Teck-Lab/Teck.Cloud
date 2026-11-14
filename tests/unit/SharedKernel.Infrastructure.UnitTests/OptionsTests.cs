using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SharedKernel.Infrastructure.Hosting;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.UnitTests;

/// <summary>Verifies opt-in host composition and source-generated startup validation.</summary>
public sealed partial class OptionsTests
{
    /// <summary>A diagnostics-only host starts without database, Redis, broker or tenant services.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task MinimalHostNeedsNoPersistenceOrTenant()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddFoundationHost();
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.IsCancellationRequested.ShouldBeTrue();
        await host.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Invalid required settings fail startup before processing work.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task GeneratedValidatorRejectsMissingSettings()
    {
        var builder = Host.CreateApplicationBuilder();
        _ = builder.Services.AddValidatedSettings<ServiceSettings, ServiceSettingsValidator>(_ => { });
        using var host = builder.Build();
        _ = (await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true)).ShouldBeOfType<OptionsValidationException>();
    }

    /// <summary>Example service-owned typed settings.</summary>
    public sealed class ServiceSettings
    {
        /// <summary>Gets or sets the required service name.</summary>
        [Required]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Build-time validation; no runtime DataAnnotations reflection.</summary>
    [OptionsValidator]
    public sealed partial class ServiceSettingsValidator : IValidateOptions<ServiceSettings>;
}
