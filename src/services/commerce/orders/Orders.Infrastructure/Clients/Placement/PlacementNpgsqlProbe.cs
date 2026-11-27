using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;
using PlacementView = Teck.Cloud.Commerce.Orders.Application.Orders.Responses.PlacementRegistration;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Clients.Placement;

/// <summary>
/// Read-only Npgsql placement probe. It verifies TLS, the actual database, primary/standby role
/// and the pre-existing tenant/order/subscription/placement marker; it never writes or provisions.
/// </summary>
internal sealed class PlacementNpgsqlProbe : IPlacementProbe
{
    private const string TargetSql =
        "SELECT current_database(), pg_is_in_recovery(), " +
        "COALESCE((SELECT ssl FROM pg_stat_ssl WHERE pid = pg_backend_pid()), FALSE)";

    private const string BindingSql =
        "SELECT 1 FROM teck_commerce.placement_binding " +
        "WHERE tenant_id = @tenant_id AND order_id = @order_id " +
        "AND subscription_id = @subscription_id AND placement_id = @placement_id LIMIT 1";

    private const string ReadOnlyOption = "-c default_transaction_read_only=on";

    private static readonly TimeSpan ProbeBudget = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan CommandBudget = TimeSpan.FromSeconds(5);

    private readonly IConfiguration _configuration;

    private readonly TimeProvider _timeProvider;

    private readonly Dictionary<string, PlacementEntry> _entries;

    /// <summary>Initializes a new instance of the <see cref="PlacementNpgsqlProbe"/> class.</summary>
    /// <param name="options">The validated placement registry.</param>
    /// <param name="configuration">The configuration source for connection-string references.</param>
    /// <param name="timeProvider">The UTC clock used for observation timestamps.</param>
    internal PlacementNpgsqlProbe(PlacementOptions options, IConfiguration configuration, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _configuration = configuration;
        _timeProvider = timeProvider;
        _entries = options.Entries.ToDictionary(static entry => entry.PlacementId, StringComparer.Ordinal);
        Registrations = [.. options.Entries.Select(static entry => entry.ToView())];
    }

    private enum TargetObservationStatus
    {
        Success = 0,
        Unavailable = 1,
        CapabilityUnavailable = 2,
    }

    /// <inheritdoc/>
    public IReadOnlyList<PlacementView> Registrations { get; }

    /// <inheritdoc/>
    public PlacementView? FindRegistration(string placementId)
        => placementId is not null && _entries.TryGetValue(placementId, out var entry) && entry is not null
            ? entry.ToView()
            : null;

    /// <inheritdoc/>
    public Task<PlacementProbeResult> VerifyAsync(
        PlacementProbeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entry = ResolveEntry(request, out var failure);
        return entry is null
            ? Task.FromResult(failure ?? Result(PlacementProbeStatus.CapabilityUnavailable, request.PlacementId, PlacementFailureCodes.CapabilityUnavailable))
            : VerifyEntryAsync(request, entry, cancellationToken);
    }

    private static bool TryValidateAddOns(IReadOnlyList<string>? addOnCodes, out bool replicaRequired)
    {
        var codes = addOnCodes ?? [];
        replicaRequired = false;
        if (codes.Any(static code => !string.Equals(code, PlacementOptions.ReplicaReadAccess, StringComparison.Ordinal)))
        {
            return false;
        }

        replicaRequired = codes.Any(static code => string.Equals(code, PlacementOptions.ReplicaReadAccess, StringComparison.Ordinal));
        return true;
    }

    private static async Task<int> SetReadOnlyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction);
        await using var lifetime = command.ConfigureAwait(false);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<TargetFacts?> ReadTargetFactsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(TargetSql, connection, transaction);
        await using var commandLifetime = command.ConfigureAwait(false);
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerLifetime = reader.ConfigureAwait(false);

        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new TargetFacts(reader.GetString(0), reader.GetBoolean(1), reader.GetBoolean(2))
            : null;
    }

    private static async Task<bool> QueryBindingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string placementId,
        PlacementProbeRequest request,
        CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(BindingSql, connection, transaction);
        await using var commandLifetime = command.ConfigureAwait(false);
        _ = command.Parameters.Add(new NpgsqlParameter("tenant_id", NpgsqlDbType.Uuid) { Value = request.TenantId });
        _ = command.Parameters.Add(new NpgsqlParameter("order_id", NpgsqlDbType.Uuid) { Value = request.OrderId });
        _ = command.Parameters.Add(new NpgsqlParameter("subscription_id", NpgsqlDbType.Uuid) { Value = request.SubscriptionId });
        _ = command.Parameters.Add(new NpgsqlParameter("placement_id", NpgsqlDbType.Text) { Value = placementId });

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null;
    }

    private static string NormalizeTier(string? tier)
        => string.IsNullOrWhiteSpace(tier) ? string.Empty : tier.Trim().ToLowerInvariant();

    private static (NpgsqlConnectionStringBuilder? Builder, TargetObservationStatus Status) BuildConnectionString(
        IConfiguration configuration,
        string secretReference)
    {
        var connectionString = configuration.GetConnectionString(secretReference);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return (null, TargetObservationStatus.Unavailable);
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(builder.Host) ||
                string.IsNullOrWhiteSpace(builder.Database) ||
                string.IsNullOrWhiteSpace(builder.Username))
            {
                return (null, TargetObservationStatus.Unavailable);
            }

            if (builder.SslMode is SslMode.Disable or SslMode.Allow or SslMode.Prefer)
            {
                return (null, TargetObservationStatus.CapabilityUnavailable);
            }

            builder.Pooling = false;
            builder.Multiplexing = false;
            builder.ApplicationName = "teck-cloud-commerce-placement-probe";
            if (builder.Timeout <= 0 || builder.Timeout > CommandBudget.Seconds)
            {
                builder.Timeout = CommandBudget.Seconds;
            }

            if (builder.CommandTimeout <= 0 || builder.CommandTimeout > CommandBudget.Seconds)
            {
                builder.CommandTimeout = CommandBudget.Seconds;
            }

            if (builder.Options?.Contains("default_transaction_read_only=on", StringComparison.OrdinalIgnoreCase) != true)
            {
                builder.Options = string.IsNullOrWhiteSpace(builder.Options)
                    ? ReadOnlyOption
                    : builder.Options + " " + ReadOnlyOption;
            }

            return (builder, TargetObservationStatus.Success);
        }
        catch (ArgumentException)
        {
            return (null, TargetObservationStatus.Unavailable);
        }
    }

    private static async Task<TargetObservation> ReadObservationAsync(
        PlacementProbeRequest request,
        string placementId,
        NpgsqlConnectionStringBuilder builder,
        bool expectStandby,
        string? expectedDatabase,
        CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(builder.ConnectionString);
        await using var connectionLifetime = connection.ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transactionLifetime = transaction.ConfigureAwait(false);

        _ = await SetReadOnlyAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var facts = await ReadTargetFactsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        if (facts is null)
        {
            return TargetObservation.Unavailable();
        }

        if (!string.Equals(facts.Database, builder.Database, StringComparison.Ordinal) ||
            (expectedDatabase is not null && !string.Equals(facts.Database, expectedDatabase, StringComparison.Ordinal)) ||
            !facts.TlsActive ||
            facts.InRecovery != expectStandby)
        {
            return TargetObservation.CapabilityUnavailable();
        }

        var bindingFound = await QueryBindingAsync(connection, transaction, placementId, request, cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return TargetObservation.Success(facts.Database, bindingFound);
    }

    private static async Task<TargetObservation> ReadObservationSafelyAsync(
        PlacementProbeRequest request,
        string placementId,
        NpgsqlConnectionStringBuilder builder,
        bool expectStandby,
        string? expectedDatabase,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ReadObservationAsync(
                request,
                placementId,
                builder,
                expectStandby,
                expectedDatabase,
                cancellationToken).ConfigureAwait(false);
        }
        catch (NpgsqlException)
        {
            return TargetObservation.Unavailable();
        }
        catch (TimeoutException)
        {
            return TargetObservation.Unavailable();
        }
        catch (ArgumentException)
        {
            return TargetObservation.Unavailable();
        }
        catch (InvalidOperationException)
        {
            return TargetObservation.Unavailable();
        }
    }

    private Task<PlacementProbeResult> VerifyEntryAsync(
        PlacementProbeRequest request,
        PlacementEntry entry,
        CancellationToken cancellationToken)
        => TryValidateAddOns(request.AddOnCodes, out var replicaRequired)
            ? ContinueVerifyAsync(request, entry, replicaRequired, cancellationToken)
            : Task.FromResult(Result(PlacementProbeStatus.CapabilityUnavailable, request.PlacementId, PlacementFailureCodes.CapabilityUnavailable));

    private Task<PlacementProbeResult> ContinueVerifyAsync(
        PlacementProbeRequest request,
        PlacementEntry entry,
        bool replicaRequired,
        CancellationToken cancellationToken)
        => replicaRequired && entry.ReplicaSecretReference is null
            ? Task.FromResult(Result(PlacementProbeStatus.CapabilityUnavailable, request.PlacementId, PlacementFailureCodes.CapabilityUnavailable))
            : ProbeAsync(request, entry, replicaRequired, cancellationToken);

    private PlacementEntry? ResolveEntry(PlacementProbeRequest request, out PlacementProbeResult? failure)
    {
        failure = null;
        if (!_entries.TryGetValue(request.PlacementId, out var entry) || entry is null)
        {
            failure = Result(PlacementProbeStatus.CapabilityUnavailable, request.PlacementId, PlacementFailureCodes.CapabilityUnavailable);
            return null;
        }

        if (!string.Equals(entry.Tier, NormalizeTier(request.Tier), StringComparison.Ordinal))
        {
            failure = Result(PlacementProbeStatus.CapabilityUnavailable, request.PlacementId, PlacementFailureCodes.CapabilityUnavailable);
            return null;
        }

        if (entry.DedicatedTenantId is Guid dedicatedTenantId && dedicatedTenantId != request.TenantId)
        {
            failure = Result(PlacementProbeStatus.BindingMismatch, request.PlacementId, PlacementFailureCodes.BindingMismatch);
            return null;
        }

        if (request.TenantId == Guid.Empty || request.OrderId == Guid.Empty || request.SubscriptionId == Guid.Empty)
        {
            failure = Result(PlacementProbeStatus.BindingMismatch, request.PlacementId, PlacementFailureCodes.BindingMismatch);
            return null;
        }

        return entry;
    }

    private async Task<PlacementProbeResult> ProbeAsync(
        PlacementProbeRequest request,
        PlacementEntry entry,
        bool replicaRequired,
        CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(ProbeBudget);
        try
        {
            var failure = await ProbeTargetsAsync(request, entry, replicaRequired, budget.Token).ConfigureAwait(false);
            return failure ?? new PlacementProbeResult(
                PlacementProbeStatus.Verified,
                request.PlacementId,
                _timeProvider.GetUtcNow(),
                FailureCode: null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result(PlacementProbeStatus.Unavailable, request.PlacementId, PlacementFailureCodes.ProbeUnavailable);
        }
    }

    private async Task<PlacementProbeResult?> ProbeTargetsAsync(
        PlacementProbeRequest request,
        PlacementEntry entry,
        bool replicaRequired,
        CancellationToken cancellationToken)
    {
        var primary = await ObserveTargetAsync(
            request,
            placementId: entry.PlacementId,
            secretReference: entry.PrimarySecretReference,
            expectStandby: false,
            expectedDatabase: null,
            cancellationToken).ConfigureAwait(false);

        return MapFailure(primary, request.PlacementId) ?? await (primary.BindingFound
                ? ReplicaResultAsync(request, entry, primary.Database, replicaRequired, cancellationToken)
                : Task.FromResult<PlacementProbeResult?>(Result(PlacementProbeStatus.BindingMismatch, request.PlacementId, PlacementFailureCodes.BindingMismatch)))
            .ConfigureAwait(false);
    }

    private Task<PlacementProbeResult?> ReplicaResultAsync(
        PlacementProbeRequest request,
        PlacementEntry entry,
        string? primaryDatabase,
        bool replicaRequired,
        CancellationToken cancellationToken)
        => replicaRequired
            ? VerifyReplicaAsync(request, entry, primaryDatabase, cancellationToken)
            : Task.FromResult<PlacementProbeResult?>(null);

    private async Task<PlacementProbeResult?> VerifyReplicaAsync(
        PlacementProbeRequest request,
        PlacementEntry entry,
        string? primaryDatabase,
        CancellationToken cancellationToken)
    {
        var replica = await ObserveTargetAsync(
            request,
            placementId: entry.PlacementId,
            secretReference: entry.ReplicaSecretReference!,
            expectStandby: true,
            expectedDatabase: primaryDatabase,
            cancellationToken).ConfigureAwait(false);

        var replicaFailure = MapFailure(replica, request.PlacementId);
        return replicaFailure ?? (replica.BindingFound
            ? null
            : Result(PlacementProbeStatus.BindingMismatch, request.PlacementId, PlacementFailureCodes.BindingMismatch));
    }

    private PlacementProbeResult? MapFailure(TargetObservation observation, string placementId)
        => observation.Status switch
        {
            TargetObservationStatus.Success => null,
            TargetObservationStatus.Unavailable => Result(PlacementProbeStatus.Unavailable, placementId, PlacementFailureCodes.ProbeUnavailable),
            TargetObservationStatus.CapabilityUnavailable => Result(PlacementProbeStatus.CapabilityUnavailable, placementId, PlacementFailureCodes.CapabilityUnavailable),
            _ => null,
        };

    private PlacementProbeResult Result(PlacementProbeStatus status, string placementId, string failureCode)
        => new(status, placementId, _timeProvider.GetUtcNow(), failureCode);

    private Task<TargetObservation> ObserveTargetAsync(
        PlacementProbeRequest request,
        string placementId,
        string secretReference,
        bool expectStandby,
        string? expectedDatabase,
        CancellationToken cancellationToken)
    {
        var (builder, status) = BuildConnectionString(_configuration, secretReference);
        return builder is null
            ? Task.FromResult(TargetObservation.Failure(status))
            : ReadObservationSafelyAsync(
                request,
                placementId,
                builder,
                expectStandby,
                expectedDatabase,
                cancellationToken);
    }

    private sealed record TargetFacts(string Database, bool InRecovery, bool TlsActive);

    private sealed record TargetObservation(TargetObservationStatus Status, string? Database, bool BindingFound)
    {
        public static TargetObservation Success(string database, bool bindingFound)
            => new(TargetObservationStatus.Success, database, bindingFound);

        public static TargetObservation Failure(TargetObservationStatus status)
            => new(status, Database: null, BindingFound: false);

        public static TargetObservation Unavailable()
            => new(TargetObservationStatus.Unavailable, Database: null, BindingFound: false);

        public static TargetObservation CapabilityUnavailable()
            => new(TargetObservationStatus.CapabilityUnavailable, Database: null, BindingFound: false);
    }
}
