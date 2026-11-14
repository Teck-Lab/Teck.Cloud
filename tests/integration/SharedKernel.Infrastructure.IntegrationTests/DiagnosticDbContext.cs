using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Model-only fixture that never opens a connection.</summary>
internal sealed class DiagnosticDbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseNpgsql("Host=localhost;Database=unused_diagnostic_fixture");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        _ = modelBuilder.Entity<DiagnosticRecord>().HasKey(record => record.Id);
        _ = modelBuilder.Entity<DiagnosticRecord>().Property(record => record.Name);
    }
}
