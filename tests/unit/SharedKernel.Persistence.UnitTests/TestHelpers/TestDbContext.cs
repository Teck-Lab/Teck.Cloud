using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence.Database.EFCore;

namespace SharedKernel.Persistence.UnitTests.TestHelpers;

/// <summary>
/// Test DbContext for repository testing.
/// </summary>
internal sealed class TestDbContext : BaseDbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    {
    }

    public DbSet<TestEntity> TestEntities => Set<TestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TestEntity>(entity =>
        {
            entity.ToTable("TestEntities");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
        });
    }
}
