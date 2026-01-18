using Catalog.Application.Suppliers.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Catalog.Infrastructure.Persistence.Config.Read;

/// <summary>
/// Configuration for <see cref="SupplierReadModel"/> entity.
/// </summary>
public class SupplierReadConfig : IEntityTypeConfiguration<SupplierReadModel>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SupplierReadModel> builder)
    {
        builder.ToTable("Suppliers");

        builder.HasKey(supplier => supplier.Id);

        builder.Property(supplier => supplier.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(supplier => supplier.Description)
            .HasMaxLength(500);

        builder.Property(supplier => supplier.WebsiteUrl)
            .HasMaxLength(255);

        builder.Property(supplier => supplier.ContactEmail)
            .HasMaxLength(100);

        builder.Property(supplier => supplier.ContactName)
            .HasMaxLength(100);

        builder.Property(supplier => supplier.ContactPhone)
            .HasMaxLength(20);

        // Apply standard audit property configurations
        builder.ConfigureAuditProperties();
    }
}
