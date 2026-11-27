using Npgsql;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Work;

/// <summary>Durably marks the provider receipt after authoritative reconciliation.</summary>
/// <param name="dataSource">The service database data source.</param>
public sealed class ProviderReceiptStore(NpgsqlDataSource dataSource)
{
    private const string CompleteSql =
        "UPDATE orders.\"ProviderEventReceipts\"\n" +
        "SET \"Status\" = 3, \"ProcessedAt\" = @now, \"Attempts\" = \"Attempts\" + 1, \"LastErrorCategory\" = NULL\n" +
        "WHERE \"Id\" = @id;";

    private const string FailSql =
        "UPDATE orders.\"ProviderEventReceipts\"\n" +
        "SET \"Status\" = 1, \"Attempts\" = \"Attempts\" + 1, \"LastErrorCategory\" = @category\n" +
        "WHERE \"Id\" = @id;";

    /// <summary>Marks the receipt as completed exactly once per processing attempt.</summary>
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

    /// <summary>Marks the receipt failed with a sanitized category while the work retries.</summary>
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
