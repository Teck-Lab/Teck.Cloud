using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Licensing.Infrastructure.Database;

namespace Teck.Cloud.Commerce.Licensing.Host;

/// <summary>Opt-in isolated-environment migration runner; production deployment owns migrations.</summary>
/// <param name="provider">The root service provider.</param>
/// <param name="configuration">Host configuration.</param>
internal sealed class LicensingDatabaseMigrator(IServiceProvider provider, IConfiguration configuration) : IHostedService
{
    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("CommerceLicensing:Database:AutoMigrate", defaultValue: false))
        {
            return;
        }

        var scope = provider.CreateAsyncScope();
        try
        {
            scope.ServiceProvider.GetRequiredService<OperationContext>().Initialize(DataScope.Global);
            var database = scope.ServiceProvider.GetRequiredService<LicensingWriteDbContext>();
            await database.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await scope.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
