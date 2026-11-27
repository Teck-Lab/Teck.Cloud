using System.Collections.Concurrent;
using System.Globalization;
using Npgsql;

namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>Bounded direct PostgreSQL assertions over committed commerce state.</summary>
internal static class Db
{
    private static readonly ConcurrentDictionary<string, NpgsqlDataSource> DataSources = new(StringComparer.Ordinal);

    internal static async Task<long> ScalarAsync(string connectionString, string sql, CancellationToken cancellationToken, params object?[] parameters)
    {
        var result = await ExecuteScalarAsync(connectionString, sql, parameters, cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    internal static async Task<string?> TextAsync(string connectionString, string sql, CancellationToken cancellationToken, params object?[] parameters)
    {
        var result = await ExecuteScalarAsync(connectionString, sql, parameters, cancellationToken).ConfigureAwait(false);
        return result as string;
    }

    internal static async Task<DateTimeOffset?> TimestampAsync(string connectionString, string sql, CancellationToken cancellationToken, params object?[] parameters)
    {
        var result = await ExecuteScalarAsync(connectionString, sql, parameters, cancellationToken).ConfigureAwait(false);
        return result is DateTime dateTime
            ? new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc))
            : null;
    }

    private static async Task<object?> ExecuteScalarAsync(
        string connectionString,
        string sql,
        object?[] parameters,
        CancellationToken cancellationToken)
    {
        var command = DataSource(connectionString).CreateCommand(sql);
        await using var commandLifetime = command.ConfigureAwait(false);
        for (var index = 0; index < parameters.Length; index++)
        {
            _ = command.Parameters.AddWithValue(parameters[index] ?? DBNull.Value);
        }

        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    private static NpgsqlDataSource DataSource(string connectionString) =>
        DataSources.GetOrAdd(connectionString, static value => NpgsqlDataSource.Create(value));
}
