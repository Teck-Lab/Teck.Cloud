namespace SharedKernel.Core.Models;

/// <summary>
/// Represents database credentials with separate admin and application users.
/// </summary>
public sealed record DatabaseCredentials
{
    /// <summary>
    /// Admin user credentials for database migrations and schema changes.
    /// </summary>
    public required UserCredentials Admin { get; init; }

    /// <summary>
    /// Application user credentials for runtime database access.
    /// </summary>
    public required UserCredentials Application { get; init; }

    /// <summary>
    /// Database host.
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// Database port.
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// Database name.
    /// </summary>
    public required string Database { get; init; }

    /// <summary>
    /// Additional connection parameters.
    /// </summary>
    public Dictionary<string, string>? AdditionalParameters { get; init; }

    /// <summary>
    /// Database provider (e.g., "PostgreSQL", "SqlServer", "MySQL").
    /// </summary>
    public string? Provider { get; init; }

    /// <summary>
    /// Gets the connection string for admin user.
    /// </summary>
    public string GetAdminConnectionString(string provider) =>
        BuildConnectionString(Admin, provider, null, null);

    /// <summary>
    /// Gets the connection string for admin user with optional host/port override.
    /// </summary>
    public string GetAdminConnectionString(string provider, string? overrideHost, int? overridePort) =>
        BuildConnectionString(Admin, provider, overrideHost, overridePort);

    /// <summary>
    /// Gets the connection string for application user.
    /// </summary>
    public string GetApplicationConnectionString(string provider) =>
        BuildConnectionString(Application, provider, null, null);

    /// <summary>
    /// Gets the connection string for application user with optional host/port override.
    /// Useful for read replicas that use different host/port but same credentials.
    /// </summary>
    public string GetApplicationConnectionString(string provider, string? overrideHost, int? overridePort) =>
        BuildConnectionString(Application, provider, overrideHost, overridePort);

    private string BuildConnectionString(UserCredentials credentials, string provider, string? overrideHost, int? overridePort)
    {
        var host = overrideHost ?? Host;
        var port = overridePort ?? Port;

        var builder = provider.ToLowerInvariant() switch
        {
            "postgresql" or "postgres" or "npgsql" => BuildPostgreSqlConnectionString(credentials, host, port),
            "sqlserver" or "mssql" => BuildSqlServerConnectionString(credentials, host, port),
            "mysql" => BuildMySqlConnectionString(credentials, host, port),
            _ => throw new NotSupportedException($"Database provider '{provider}' is not supported."),
        };

        if (AdditionalParameters is not null)
        {
            foreach (var (key, value) in AdditionalParameters)
            {
                builder.Append($"{key}={value};");
            }
        }

        return builder.ToString();
    }

    private System.Text.StringBuilder BuildPostgreSqlConnectionString(UserCredentials credentials, string host, int port)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append($"Host={host};");
        builder.Append($"Port={port};");
        builder.Append($"Database={Database};");
        builder.Append($"Username={credentials.Username};");
        builder.Append($"Password={credentials.Password};");
        return builder;
    }

    private System.Text.StringBuilder BuildSqlServerConnectionString(UserCredentials credentials, string host, int port)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append($"Server={host},{port};");
        builder.Append($"Database={Database};");
        builder.Append($"User Id={credentials.Username};");
        builder.Append($"Password={credentials.Password};");
        builder.Append("TrustServerCertificate=True;");
        return builder;
    }

    private System.Text.StringBuilder BuildMySqlConnectionString(UserCredentials credentials, string host, int port)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append($"Server={host};");
        builder.Append($"Port={port};");
        builder.Append($"Database={Database};");
        builder.Append($"Uid={credentials.Username};");
        builder.Append($"Pwd={credentials.Password};");
        return builder;
    }
}

/// <summary>
/// User credentials for database access.
/// </summary>
public sealed record UserCredentials
{
    /// <summary>
    /// Username.
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// Password.
    /// </summary>
    public required string Password { get; init; }
}