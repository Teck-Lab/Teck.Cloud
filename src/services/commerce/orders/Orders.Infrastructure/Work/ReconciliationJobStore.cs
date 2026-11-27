using Npgsql;
using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Work;

/// <summary>Claims due provider work with PostgreSQL row locks and SKIP LOCKED.</summary>
/// <param name="dataSource">The service database data source.</param>
public sealed class ReconciliationJobStore(NpgsqlDataSource dataSource)
{
    private const string ClaimSql =
        "WITH due AS (\n" +
        "    SELECT j.\"Id\"\n" +
        "    FROM orders.\"ReconciliationJobs\" AS j\n" +
        "    WHERE j.\"Status\" = 1\n" +
        "      AND j.\"IsDeleted\" = FALSE\n" +
        "      AND j.\"DueAt\" <= @now\n" +
        "      AND (j.\"LeaseUntil\" IS NULL OR j.\"LeaseUntil\" <= @now)\n" +
        "    ORDER BY j.\"DueAt\", j.\"OrderId\"\n" +
        "    LIMIT @batch\n" +
        "    FOR UPDATE SKIP LOCKED\n" +
        ")\n" +
        "UPDATE orders.\"ReconciliationJobs\" AS j\n" +
        "SET \"LeaseToken\" = @token, \"LeaseUntil\" = @leaseUntil, \"UpdatedOn\" = @now\n" +
        "FROM due\n" +
        "WHERE j.\"Id\" = due.\"Id\"\n" +
        "RETURNING j.\"Id\", j.\"TenantId\", j.\"OrderId\", j.\"Kind\", j.\"Attempts\", j.\"ProviderEventReceiptId\";";

    /// <summary>Claims a bounded due batch under fresh leases.</summary>
    /// <param name="now">The current UTC instant.</param>
    /// <param name="token">The lease token.</param>
    /// <param name="leaseUntil">The lease expiry.</param>
    /// <param name="batchSize">The maximum batch size.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The claimed jobs.</returns>
    public async Task<IReadOnlyList<JobClaim>> ClaimAsync(
        DateTimeOffset now,
        Guid token,
        DateTimeOffset leaseUntil,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var claims = new List<JobClaim>();
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var connectionLifetime = connection.ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transactionLifetime = transaction.ConfigureAwait(false);
        var command = new NpgsqlCommand(ClaimSql, connection, transaction);
        await using var commandLifetime = command.ConfigureAwait(false);
        _ = command.Parameters.AddWithValue(nameof(now), now);
        _ = command.Parameters.AddWithValue(nameof(token), token);
        _ = command.Parameters.AddWithValue(nameof(leaseUntil), leaseUntil);
        _ = command.Parameters.AddWithValue("batch", batchSize);
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var providerEventReceiptId = await reader.IsDBNullAsync(5, cancellationToken).ConfigureAwait(false) ? (Guid?)null : reader.GetGuid(5);
                claims.Add(new JobClaim(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetGuid(2),
                    (ReconciliationKind)reader.GetInt32(3),
                    reader.GetInt32(4),
                    providerEventReceiptId,
                    token));
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return claims;
    }
}
