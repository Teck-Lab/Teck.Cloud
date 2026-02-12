using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence.Database.EFCore;

namespace SharedKernel.Persistence.UnitTests.TestHelpers;

/// <summary>
/// Test DbContext for read repository testing.
/// </summary>
internal sealed class TestReadDbContext : BaseDbContext
{
    public TestReadDbContext(DbContextOptions<TestReadDbContext> options) : base(options)
    {
    }

    public DbSet<TestReadModel> TestReadModels => Set<TestReadModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TestReadModel>(entity =>
        {
            entity.ToTable("TestReadModels");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Category).HasMaxLength(100);
        });
    }
}
