using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Catalog.Infrastructure.DesignTime;

/// <summary>
/// Design-time factory for ApplicationWriteDbContext for the Catalog service.
/// Copied from the centralized migration pattern and adjusted for the service infrastructure.
/// </summary>
public class CatalogDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationWriteDbContext>
{
    /// <summary>
    /// Creates a new instance of <see cref="ApplicationWriteDbContext"/> for design-time operations.
    /// </summary>
    /// <param name="args">Arguments passed by the design-time tool.</param>
    /// <returns>A configured <see cref="ApplicationWriteDbContext"/> instance.</returns>
    public ApplicationWriteDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationWriteDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("MIGRATION_CONNECTION_STRING") ??
            "Server=localhost;Database=Teck_catalog;User=root;Password=root;";

        // Allow explicit override of server type via env var
        var serverType = Environment.GetEnvironmentVariable("MIGRATION_SERVER_TYPE")?.ToLowerInvariant();
        if (string.IsNullOrEmpty(serverType))
        {
            // Try to infer from connection string
            if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) || connectionString.Contains("Username=", StringComparison.OrdinalIgnoreCase))
                serverType = "postgres";
            else if (connectionString.Contains("Trusted_Connection", StringComparison.OrdinalIgnoreCase) || connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) || connectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase))
                serverType = "sqlserver";
            else if (connectionString.Contains("mariadb", StringComparison.OrdinalIgnoreCase))
                serverType = "mariadb";
            else
                serverType = "mysql";
        }

        switch (serverType)
        {
            case "sqlserver":
            case "mssql":
                optionsBuilder.UseSqlServer(connectionString);
                break;
            case "postgres":
            case "pgsql":
            case "npgsql":
                optionsBuilder.UseNpgsql(connectionString);
                break;
            case "mariadb":
                optionsBuilder.UseMySQL(connectionString);
                break;
            default:
                // default to MySQL
                optionsBuilder.UseMySQL(connectionString);
                break;
        }

        return new ApplicationWriteDbContext(optionsBuilder.Options);
    }
}
