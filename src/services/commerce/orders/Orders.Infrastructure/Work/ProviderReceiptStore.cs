using Npgsql;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Work;

/// <summary>Durably marks the provider receipt after authoritative reconciliation.</summary>
/// <param name="dataSource">The service database data source.</param>
public sealed class ProviderReceiptStore(NpgsqlDataSource dataSource)
{
    private const int MaximumOutstanding = 50;

    private const string CompleteSql =
        "UPDATE orders.\"ProviderEventReceipts\"\n" +
        "SET \"Status\" = 3, \"ProcessedAt\" = @now, \"Attempts\" = \"Attempts\" + 1, \"LastErrorCategory\" = NULL\n" +
        "WHERE \"Id\" = @id AND \"Status\" <> 3;";

    private const string FailSql =
        "UPDATE orders.\"ProviderEventReceipts\"\n" +
        "SET \"Status\" = 1, \"Attempts\" = \"Attempts\" + 1, \"LastErrorCategory\" = @category\n" +
        "WHERE \"Id\" = @id AND \"Status\" <> 3;";

    private const string OutstandingSql =
        "SELECT \"Id\" FROM orders.\"ProviderEventReceipts\"\n" +
        "WHERE \"TenantId\" = @tenant AND \"OrderId\" = @order AND \"IsDeleted\" = FALSE AND \"Status\" <> 3\n" +
        "ORDER BY \"ReceivedAt\"\n" +
        "LIMIT @limit;";

    /// <summary>Lists the bounded outstanding receipt ids bound to one explicit tenant and order before a provider call.</summary>
    /// <param name="tenantId">The explicit tenant id.</param>
    /// <param name="orderId">The bound order id.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The outstanding receipt ids, oldest first; empty when none remain.</returns>
    public async Task<IReadOnlyList<Guid>> ListOutstandingAsync(string tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        var ids = new List<Guid>();
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var connectionLifetime = connection.ConfigureAwait(false);
        var command = new NpgsqlCommand(OutstandingSql, connection);
        await using var commandLifetime = command.ConfigureAwait(false);
        _ = command.Parameters.AddWithValue("tenant", tenantId);
        _ = command.Parameters.AddWithValue("order", orderId);
        _ = command.Parameters.AddWithValue("limit", MaximumOutstanding);
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerLifetime = reader.ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    /// <summary>Marks the receipt as completed exactly once and never downgrades an already completed receipt.</summary>
    /// <param name="receiptId">The receipt id.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The operation task.</returns>
    public async Task MarkCompletedAsync(Guid receiptId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var connectionLifetime = connection.ConfigureAwait(false);
        var command = new NpgsqlCommand(CompleteSql, connection);
        await using var commandLifetime = command.ConfigureAwait(false);
        _ = command.Parameters.AddWithValue("id", receiptId);
        _ = command.Parameters.AddWithValue(nameof(now), now);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Marks an outstanding receipt failed with a sanitized category while the work retries; completed receipts are never downgraded.</summary>
    /// <param name="receiptId">The receipt id.</param>
    /// <param name="category">The sanitized category.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The operation task.</returns>
    public async Task MarkFailedAsync(Guid receiptId, string category, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var connectionLifetime = connection.ConfigureAwait(false);
        var command = new NpgsqlCommand(FailSql, connection);
        await using var commandLifetime = command.ConfigureAwait(false);
        _ = command.Parameters.AddWithValue("id", receiptId);
        _ = command.Parameters.AddWithValue(nameof(category), category);
        _ = command.Parameters.AddWithValue(nameof(now), now);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
