using System.Data.Common;

namespace SharedKernel.Core.Database;

/// <summary>
/// Represents database credentials with separate admin and application users.
/// </summary>
public sealed record DatabaseCredentials
{
    /// <summary>
    /// Gets admin user credentials for database migrations and schema changes.
    /// </summary>
    public required UserCredentials Admin { get; init; }

    /// <summary>
    /// Gets application user credentials for runtime database access.
    /// </summary>
    public required UserCredentials Application { get; init; }

    /// <summary>
    /// Gets database host.
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// Gets database port.
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// Gets database name.
    /// </summary>
    public required string Database { get; init; }

    /// <summary>
    /// Gets additional connection parameters.
    /// </summary>
    public IReadOnlyDictionary<string, string>? AdditionalParameters { get; init; }

    /// <summary>
    /// Gets the connection string for admin user.
    /// </summary>
    /// <returns>The connection string for the admin user.</returns>
    public string GetAdminConnectionString() =>
        BuildConnectionString(Admin, overrideHost: null, overridePort: null);

    /// <summary>
    /// Gets the connection string for admin user with optional host/port override.
    /// </summary>
    /// <param name="overrideHost">An optional host that replaces the configured host.</param>
    /// <param name="overridePort">An optional port that replaces the configured port.</param>
    /// <returns>The connection string for the admin user with the supplied overrides applied.</returns>
    public string GetAdminConnectionString(string? overrideHost, int? overridePort) =>
        BuildConnectionString(Admin, overrideHost, overridePort);

    /// <summary>
    /// Gets the connection string for application user.
    /// </summary>
    /// <returns>The connection string for the application user.</returns>
    public string GetApplicationConnectionString() =>
        BuildConnectionString(Application, overrideHost: null, overridePort: null);

    /// <summary>
    /// Gets the connection string for application user with optional host/port override.
    /// Useful for read replicas that use different host/port but same credentials.
    /// </summary>
    /// <param name="overrideHost">An optional host that replaces the configured host.</param>
    /// <param name="overridePort">An optional port that replaces the configured port.</param>
    /// <returns>The connection string for the application user with the supplied overrides applied.</returns>
    public string GetApplicationConnectionString(string? overrideHost, int? overridePort) =>
        BuildConnectionString(Application, overrideHost, overridePort);

    /// <inheritdoc/>
    public override string ToString() => nameof(DatabaseCredentials) + " { [REDACTED] }";

    private string BuildConnectionString(UserCredentials credentials, string? overrideHost, int? overridePort)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var builder = new DbConnectionStringBuilder
        {
            ["Host"] = overrideHost ?? Host,
            ["Port"] = overridePort ?? Port,
            ["Database"] = Database,
            ["Username"] = credentials.Username,
            ["Password"] = credentials.Password,
        };

        if (AdditionalParameters is not null)
        {
            foreach (var (key, value) in AdditionalParameters)
            {
                if (builder.ContainsKey(key) ||
                    key.Equals("User ID", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("User Name", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("Server", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Additional parameters cannot override connection identity.");
                }

                builder[key] = value;
            }
        }

        return builder.ConnectionString;
    }
}
