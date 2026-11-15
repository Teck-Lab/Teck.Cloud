using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using SharedKernel.Persistence.Database;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.UnitTests;

/// <summary>Proves EF scans distinct read/write model configurations.</summary>
public sealed class ModelConfigurationFilterTests
{
    /// <summary>Write and read scans include shared mappings but exclude the other side.</summary>
    [Fact]
    public void ReadAndWriteContextsApplyOnlyTheirMappings()
    {
        _ = typeof(SharedMapping);
        _ = typeof(WriteMapping);
        _ = typeof(ReadMapping);
        var write = new ModelBuilder(new ConventionSet());
        var read = new ModelBuilder(new ConventionSet());
        _ = write.ApplyConfigurationsFromAssembly(typeof(ModelConfigurationFilterTests).Assembly, ModelConfigurationFilters.WriteConfigurationsFilter);
        _ = read.ApplyConfigurationsFromAssembly(typeof(ModelConfigurationFilterTests).Assembly, ModelConfigurationFilters.ReadConfigurationsFilter);

        _ = write.Model.FindEntityType(typeof(SharedEntity)).ShouldNotBeNull();
        _ = write.Model.FindEntityType(typeof(WriteEntity)).ShouldNotBeNull();
        write.Model.FindEntityType(typeof(ReadEntity)).ShouldBeNull();
        _ = read.Model.FindEntityType(typeof(SharedEntity)).ShouldNotBeNull();
        _ = read.Model.FindEntityType(typeof(ReadEntity)).ShouldNotBeNull();
        read.Model.FindEntityType(typeof(WriteEntity)).ShouldBeNull();
    }

    internal sealed class SharedEntity
    {
        public int Id { get; set; }
    }

    internal sealed class WriteEntity
    {
        public int Id { get; set; }
    }

    internal sealed class ReadEntity
    {
        public int Id { get; set; }
    }

    internal sealed class SharedMapping : ISharedModelConfiguration<SharedEntity>
    {
        public void Configure(EntityTypeBuilder<SharedEntity> builder) => builder.HasKey(entity => entity.Id);
    }

    internal sealed class WriteMapping : IWriteModelConfiguration<WriteEntity>
    {
        public void Configure(EntityTypeBuilder<WriteEntity> builder) => builder.HasKey(entity => entity.Id);
    }

    internal sealed class ReadMapping : IReadModelConfiguration<ReadEntity>
    {
        public void Configure(EntityTypeBuilder<ReadEntity> builder) => builder.HasKey(entity => entity.Id);
    }
}
