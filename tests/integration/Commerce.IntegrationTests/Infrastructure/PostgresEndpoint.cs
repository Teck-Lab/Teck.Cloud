namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>A disposable PostgreSQL endpoint with TLS verification disabled only for the isolated fixture.</summary>
/// <param name="ConnectionString">The read/write fixture connection.</param>
/// <param name="AdminConnectionString">The fixture owner connection.</param>
/// <param name="Host">The mapped host.</param>
/// <param name="Port">The mapped port.</param>
/// <param name="Database">The database.</param>
public sealed record PostgresEndpoint(string ConnectionString, string AdminConnectionString, string Host, int Port, string Database);
