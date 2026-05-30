// <copyright file="BasketDesignTimeDbContextFactory.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Basket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Basket.Infrastructure.DesignTime;

/// <summary>
/// Design-time factory for <see cref="BasketPersistenceDbContext"/> for the Basket service.
/// </summary>
public class BasketDesignTimeDbContextFactory : IDesignTimeDbContextFactory<BasketPersistenceDbContext>
{
    /// <summary>
    /// Creates a new instance of <see cref="BasketPersistenceDbContext"/> for design-time operations.
    /// </summary>
    /// <param name="args">Arguments passed by the design-time tool.</param>
    /// <returns>A configured <see cref="BasketPersistenceDbContext"/> instance.</returns>
    public BasketPersistenceDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BasketPersistenceDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("MIGRATION_CONNECTION_STRING") ??
            "Host=localhost;Database=teck_migrations;Username=postgres;Password=postgres";

        var serverType = Environment.GetEnvironmentVariable("MIGRATION_SERVER_TYPE")?.ToUpperInvariant();
        if (string.IsNullOrEmpty(serverType))
        {
            if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
                connectionString.Contains("Username=", StringComparison.OrdinalIgnoreCase))
            {
                serverType = "POSTGRES";
            }
            else if (connectionString.Contains("Trusted_Connection", StringComparison.OrdinalIgnoreCase) ||
                connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) ||
                connectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase))
            {
                serverType = "SQLSERVER";
            }
            else
            {
                serverType = "MYSQL";
            }
        }

        switch (serverType)
        {
            case "SQLSERVER":
            case "MSSQL":
                optionsBuilder.UseSqlServer(connectionString);
                break;
            case "POSTGRES":
            case "PGSQL":
            case "NPGSQL":
                optionsBuilder.UseNpgsql(connectionString);
                break;
            default:
                optionsBuilder.UseMySQL(connectionString);
                break;
        }

        return new BasketPersistenceDbContext(optionsBuilder.Options);
    }
}
