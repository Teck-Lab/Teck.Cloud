using Npgsql;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Work;

/// <summary>Lists expired enabled entitlements for reconciliation before network work.</summary>
/// <param name="dataSource">The service database data source.</param>
public sealed class ExpiredEntitlementStore(NpgsqlDataSource dataSource)
{
    private const string DueSql =
        "SELECT \"TenantId\", \"Id\"\n" +
        "FROM orders.\"Orders\"\n" +
        "WHERE \"IsDeleted\" = FALSE\n" +
        "  AND \"DesiredEnabled\" = TRUE\n" +
        "  AND (\"DesiredNotAfter\" IS NULL OR \"DesiredNotAfter\" <= @now)\n" +
        "ORDER BY \"DesiredNotAfter\"\n" +
        "LIMIT @batch;";

    /// <summary>Lists a bounded set of orders whose desired entitlement has expired.</summary>
    /// <param name="now">The current UTC instant.</param>
    /// <param name="batchSize">The maximum batch size.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>Tenant and order pairs.</returns>
    public async Task<IReadOnlyList<(string TenantId, Guid OrderId)>> ListDueAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var rows = new List<(string, Guid)>();
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var connectionLifetime = connection.ConfigureAwait(false);
        var command = new NpgsqlCommand(DueSql, connection);
        await using var commandLifetime = command.ConfigureAwait(false);
        _ = command.Parameters.AddWithValue(nameof(now), now);
        _ = command.Parameters.AddWithValue("batch", batchSize);
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerLifetime = reader.ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add((reader.GetString(0), reader.GetGuid(1)));
        }

        return rows;
    }
}
