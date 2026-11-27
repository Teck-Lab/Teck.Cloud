using Npgsql;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Work;

/// <summary>Reads order facts for out-of-handler provider workers without EF filters.</summary>
/// <param name="dataSource">The service database data source.</param>
public sealed class OrderWorkStore(NpgsqlDataSource dataSource)
{
    private const string Columns =
        "\"Id\", \"TenantId\", \"SubscriptionId\", \"OfferId\", \"Tier\", \"AddOnCodesCsv\", " +
        "\"CatalogPriceSnapshot\", \"VerifiedPriceSnapshot\", \"CheckoutProviderSessionId\", \"ProviderSubscriptionId\", " +
        "\"CheckoutRequestedAt\", \"CheckoutCreateAttempts\", \"PaymentStatus\", \"PaidThrough\", " +
        "\"ApprovalStatus\", \"DesiredEnabled\", \"DesiredNotAfter\", \"DesiredRevision\"";

    private const string LoadSql =
        $"SELECT {Columns} FROM orders.\"Orders\" " +
        "WHERE \"TenantId\" = @tenant AND \"Id\" = @order AND \"IsDeleted\" = FALSE;";

    private const string ProviderSql =
        $"SELECT {Columns} FROM orders.\"Orders\" " +
        "WHERE \"IsDeleted\" = FALSE AND (" +
        "(@order IS NOT NULL AND \"Id\" = @order) " +
        "OR (@session IS NOT NULL AND \"CheckoutProviderSessionId\" = @session) " +
        "OR (@subscription IS NOT NULL AND \"ProviderSubscriptionId\" = @subscription) " +
        "OR (@invoice IS NOT NULL AND \"ProviderInvoiceId\" = @invoice) " +
        "OR (@charge IS NOT NULL AND \"ProviderChargeId\" = @charge) " +
        "OR (@intent IS NOT NULL AND \"ProviderPaymentIntentId\" = @intent)) " +
        "LIMIT 2;";

    /// <summary>Loads one tenant-scoped order fact snapshot.</summary>
    /// <param name="tenantId">The explicit tenant id.</param>
    /// <param name="orderId">The order id.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The facts, or null when absent.</returns>
    public async Task<OrderWorkFact?> LoadAsync(string tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var connectionLifetime = connection.ConfigureAwait(false);
        var command = new NpgsqlCommand(LoadSql, connection);
        await using var commandLifetime = command.ConfigureAwait(false);
        _ = command.Parameters.AddWithValue("tenant", tenantId);
        _ = command.Parameters.AddWithValue("order", orderId);
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerLifetime = reader.ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
    }

    /// <summary>Resolves exactly one order by stored provider references only.</summary>
    /// <param name="notice">The verified provider notice.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The facts when exactly one bound order matches; otherwise null.</returns>
    public Task<OrderWorkFact?> FindByProviderAsync(BillingNotice notice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notice);
        return FindByProviderCoreAsync(notice, cancellationToken);
    }

    private static OrderWorkFact Read(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetGuid(2),
        reader.GetString(3),
        reader.GetInt32(4),
        reader.GetString(5),
        reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        reader.IsDBNull(8) ? null : reader.GetString(8),
        reader.IsDBNull(9) ? null : reader.GetString(9),
        reader.GetFieldValue<DateTimeOffset>(10),
        reader.GetInt32(11),
        reader.GetInt32(12),
        reader.IsDBNull(13) ? null : reader.GetFieldValue<DateTimeOffset>(13),
        reader.GetInt32(14),
        reader.GetBoolean(15),
        reader.IsDBNull(16) ? null : reader.GetFieldValue<DateTimeOffset>(16),
        (uint)reader.GetInt64(17));

    private async Task<OrderWorkFact?> FindByProviderCoreAsync(BillingNotice notice, CancellationToken cancellationToken)
    {
        var rows = new List<OrderWorkFact>(2);
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var connectionLifetime = connection.ConfigureAwait(false);
        var command = new NpgsqlCommand(ProviderSql, connection);
        await using var commandLifetime = command.ConfigureAwait(false);
        _ = command.Parameters.AddWithValue("order", (object?)notice.OrderId ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("session", (object?)notice.CheckoutSessionId ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("subscription", (object?)notice.ProviderSubscriptionId ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("invoice", (object?)notice.InvoiceId ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("charge", (object?)notice.ChargeId ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("intent", (object?)notice.PaymentIntentId ?? DBNull.Value);
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerLifetime = reader.ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(Read(reader));
        }

        return rows.Count == 1 ? rows[0] : null;
    }
}
