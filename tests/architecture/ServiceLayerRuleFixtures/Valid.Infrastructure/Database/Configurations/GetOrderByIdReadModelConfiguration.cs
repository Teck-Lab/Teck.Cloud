using ArchitectureRuleFixtures.Valid.Application.Sales.ReadModels;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database;

namespace ArchitectureRuleFixtures.Valid.Infrastructure.Database.Configurations;

/// <summary>Valid read-side EF mapping included in the Infrastructure architecture fixture.</summary>
public sealed class GetOrderByIdReadModelConfiguration : IReadModelConfiguration<GetOrderByIdReadModel>
{
    /// <summary>Maps the read model primary key.</summary>
    /// <param name="builder">The EF entity builder.</param>
    public void Configure(EntityTypeBuilder<GetOrderByIdReadModel> builder) => builder.HasKey(model => model.Id);
}
