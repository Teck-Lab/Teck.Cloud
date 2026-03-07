// <copyright file="TenantReadConfig.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Tenants.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Customer.Infrastructure.Persistence.Config.Read;

/// <summary>
/// Provides Entity Framework configuration for the <see cref="TenantReadModel"/> entity.
/// </summary>
public class TenantReadConfig : IEntityTypeConfiguration<TenantReadModel>
{
    /// <summary>
    /// Configures the TenantReadModel entity type.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the TenantReadModel entity.</param>
    public void Configure(EntityTypeBuilder<TenantReadModel> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        ConfigureTable(builder);
        ConfigureCoreColumns(builder);
        ConfigureOptionalColumns(builder);
    }

    private static void ConfigureTable(EntityTypeBuilder<TenantReadModel> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(tenant => tenant.Id);
    }

    private static void ConfigureCoreColumns(EntityTypeBuilder<TenantReadModel> builder)
    {
        builder.Property(tenant => tenant.Identifier).HasMaxLength(100).IsRequired();
        builder.Property(tenant => tenant.Name).HasMaxLength(200).IsRequired();
        builder.Property(tenant => tenant.Plan).HasMaxLength(50).IsRequired();
        builder.Property(tenant => tenant.KeycloakOrganizationId).HasMaxLength(64);
        builder.Property(tenant => tenant.DatabaseStrategy).HasMaxLength(50).IsRequired();
        builder.Property(tenant => tenant.DatabaseProvider).HasMaxLength(50).IsRequired();
    }

    private static void ConfigureOptionalColumns(EntityTypeBuilder<TenantReadModel> builder)
    {
        builder.Property(tenant => tenant.IsActive).IsRequired();
        builder.ConfigureAuditProperties();
    }
}
