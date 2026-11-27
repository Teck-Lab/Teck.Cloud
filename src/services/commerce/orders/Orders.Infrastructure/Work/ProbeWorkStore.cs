using Npgsql;
using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Work;

/// <summary>Claims due probe work with PostgreSQL row locks and SKIP LOCKED.</summary>
/// <param name="dataSource">The service database data source.</param>
public sealed class ProbeWorkStore(NpgsqlDataSource dataSource)
{
    private const string ClaimSql =
        "WITH due AS (\n" +
        "    SELECT w.\"Id\"\n" +
        "    FROM orders.\"PlacementProbeWork\" AS w\n" +
        "    JOIN orders.\"Orders\" AS o ON o.\"Id\" = w.\"OrderId\" AND o.\"TenantId\" = w.\"TenantId\"\n" +
        "    WHERE w.\"Terminal\" = FALSE\n" +
        "      AND w.\"IsDeleted\" = FALSE\n" +
        "      AND o.\"IsDeleted\" = FALSE\n" +
        "      AND w.\"DueAt\" IS NOT NULL\n" +
        "      AND w.\"DueAt\" <= @now\n" +
        "      AND (w.\"LeaseUntil\" IS NULL OR w.\"LeaseUntil\" <= @now)\n" +
        "      AND w.\"EvidenceRevision\" = o.\"EvidenceRevision\"\n" +
        "      AND o.\"PaymentStatus\" = 2\n" +
        "      AND o.\"PaidThrough\" > @now\n" +
        "      AND o.\"ApprovalStatus\" = 2\n" +
        "    ORDER BY w.\"DueAt\", w.\"OrderId\"\n" +
        "    LIMIT @batch\n" +
        "    FOR UPDATE OF w SKIP LOCKED\n" +
        ")\n" +
        "UPDATE orders.\"PlacementProbeWork\" AS w\n" +
        "SET \"LeaseToken\" = @token, \"LeaseUntil\" = @leaseUntil, \"UpdatedOn\" = @now\n" +
        "FROM due\n" +
        "WHERE w.\"Id\" = due.\"Id\"\n" +
        "RETURNING w.\"Id\", w.\"TenantId\", w.\"OrderId\", w.\"EvidenceRevision\", w.\"PlacementId\";";

    private const string OrderFactsSql =
        "SELECT \"OrderId\", \"SubscriptionId\", \"Tier\", \"AddOnCodesCsv\"\n" +
        "FROM orders.\"Orders\"\n" +
        "WHERE \"Id\" = ANY(@ids) AND \"IsDeleted\" = FALSE;";

    /// <summary>Claims a bounded due batch under fresh leases.</summary>
    /// <param name="now">The current UTC instant.</param>
    /// <param name="token">The lease token.</param>
    /// <param name="leaseUntil">The lease expiry.</param>
    /// <param name="batchSize">The maximum batch size.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The claimed items.</returns>
    public async Task<IReadOnlyList<ProbeWorkClaim>> ClaimAsync(
        DateTimeOffset now,
        Guid token,
        DateTimeOffset leaseUntil,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var claims = new List<ProbeWorkClaim>();
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
                claims.Add(new ProbeWorkClaim(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetGuid(2),
                    Guid.Empty,
                    (uint)reader.GetInt64(3),
                    reader.GetString(4),
                    (int)Tier.Shared,
                    string.Empty));
            }
        }

        if (claims.Count > 0)
        {
            var facts = new NpgsqlCommand(OrderFactsSql, connection, transaction);
            await using var factsLifetime = facts.ConfigureAwait(false);
            _ = facts.Parameters.AddWithValue("ids", claims.Select(static claim => claim.OrderId).ToArray());
            var byOrder = new Dictionary<Guid, (Guid SubscriptionId, int Tier, string AddOns)>();
            var factsReader = await facts.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (factsReader.ConfigureAwait(false))
            {
                while (await factsReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    byOrder[factsReader.GetGuid(0)] = (factsReader.GetGuid(1), factsReader.GetInt32(2), factsReader.GetString(3));
                }
            }

            claims = [.. claims.Where(claim => byOrder.ContainsKey(claim.OrderId)).Select(claim => claim with { SubscriptionId = byOrder[claim.OrderId].SubscriptionId, Tier = byOrder[claim.OrderId].Tier, AddOnCodesCsv = byOrder[claim.OrderId].AddOns })];
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return claims;
    }
}
