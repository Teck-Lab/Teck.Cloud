using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace Teck.Cloud.Commerce.Orders.Infrastructure;

/// <summary>Process-independent PostgreSQL advisory locks for one checkout intent scope.</summary>
/// <param name="dataSource">The service database data source.</param>
public sealed class AdvisoryCheckoutIntentLock(NpgsqlDataSource dataSource) : Application.Orders.Checkout.ICheckoutIntentLock
{
    /// <inheritdoc/>
    public Task<IAsyncDisposable> AcquireAsync(string tenantId, string subjectId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        return AcquireCoreAsync(tenantId, subjectId, cancellationToken);
    }

    private async Task<IAsyncDisposable> AcquireCoreAsync(string tenantId, string subjectId, CancellationToken cancellationToken)
    {
        var key = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(tenantId + "\u001F" + subjectId)));
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var command = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", connection);
            await using var commandLifetime = command.ConfigureAwait(false);
            _ = command.Parameters.AddWithValue("key", key);
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new Releaser(connection, key);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private sealed class Releaser(NpgsqlConnection connection, long key) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", connection);
                await using var commandLifetime = command.ConfigureAwait(false);
                _ = command.Parameters.AddWithValue("key", key);
                _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            finally
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
