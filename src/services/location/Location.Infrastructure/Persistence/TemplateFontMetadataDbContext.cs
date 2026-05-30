// <copyright file="TemplateFontMetadataDbContext.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Infrastructure.MultiTenant;
using SharedKernel.Persistence.Database.EFCore;

namespace Location.Infrastructure.Persistence;

/// <summary>
/// EF Core write context for template font metadata and display model definitions.
/// </summary>
/// <param name="options">DbContext options.</param>
/// <param name="tenantAccessor">Finbuckle multi-tenant accessor; null during design-time and code generation.</param>
public sealed class TemplateFontMetadataDbContext(
    DbContextOptions<TemplateFontMetadataDbContext> options,
    IMultiTenantContextAccessor<TenantDetails>? tenantAccessor = null)
    : BaseDbContext(options, tenantAccessor: tenantAccessor)
{
    internal DbSet<TemplateFontMetadataRecord> TemplateFonts => this.Set<TemplateFontMetadataRecord>();

    internal DbSet<DisplayModelRecord> DisplayModels => this.Set<DisplayModelRecord>();

    internal DbSet<LocationNodeRecord> LocationNodes => this.Set<LocationNodeRecord>();

    internal DbSet<TemplateDesignRecord> TemplateDesigns => this.Set<TemplateDesignRecord>();

    internal DbSet<TemplateScopeSettingsRecord> TemplateScopeSettings => this.Set<TemplateScopeSettingsRecord>();

    internal DbSet<LocationGroupRecord> LocationGroups => this.Set<LocationGroupRecord>();

    internal DbSet<TemplateAssetRecord> TemplateAssets => this.Set<TemplateAssetRecord>();

    /// <summary>
    /// Configures persistence schema mappings for Location metadata tables.
    /// </summary>
    /// <param name="modelBuilder">Model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        var entity = modelBuilder.Entity<TemplateFontMetadataRecord>();
        entity.ToTable("template_font_assets");

        entity.HasKey(record => record.Id);

        entity.Property(record => record.TenantId)
            .HasMaxLength(200)
            .IsRequired();

        entity.Property(record => record.TemplateId)
            .HasMaxLength(200)
            .IsRequired();

        entity.Property(record => record.FontKey)
            .HasMaxLength(400)
            .IsRequired();

        entity.Property(record => record.ObjectKey)
            .HasMaxLength(800)
            .IsRequired();

        entity.Property(record => record.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(record => record.OriginalFileName)
            .HasMaxLength(260)
            .IsRequired();

        entity.Property(record => record.ChecksumSha256)
            .HasMaxLength(128)
            .IsRequired();

        entity.Property(record => record.SizeBytes)
            .IsRequired();

        entity.Property(record => record.UpdatedAtUtc)
            .IsRequired();

        entity.HasIndex(record => new { record.TenantId, record.TemplateId, record.FontKey })
            .IsUnique();

        entity.HasIndex(record => new { record.TenantId, record.FontKey });

        var displayModelEntity = modelBuilder.Entity<DisplayModelRecord>();
        displayModelEntity.ToTable("display_models");

        displayModelEntity.HasKey(record => record.Id);

        displayModelEntity.Property(record => record.TenantId)
            .HasMaxLength(200)
            .IsRequired();

        displayModelEntity.Property(record => record.DisplayModelId)
            .HasMaxLength(200)
            .IsRequired();

        displayModelEntity.Property(record => record.Name)
            .HasMaxLength(200)
            .IsRequired();

        displayModelEntity.Property(record => record.Width)
            .IsRequired();

        displayModelEntity.Property(record => record.Height)
            .IsRequired();

        displayModelEntity.Property(record => record.UpdatedAtUtc)
            .IsRequired();

        displayModelEntity.HasIndex(record => new { record.TenantId, record.DisplayModelId })
            .IsUnique();

        displayModelEntity.HasIndex(record => record.TenantId);

        var locationNodeEntity = modelBuilder.Entity<LocationNodeRecord>();
        locationNodeEntity.ToTable("location_nodes");

        // Surrogate GUID PK — allows the same LocationNodeId string per tenant.
        locationNodeEntity.HasKey(record => record.Id);

        locationNodeEntity.Property(record => record.Id)
            .ValueGeneratedOnAdd();

        // Finbuckle shadow TenantId + global query filter — must be called before index definitions
        // that reference the shadow "TenantId" column.
        locationNodeEntity.IsMultiTenant();

        locationNodeEntity.Property(record => record.LocationNodeId)
            .HasMaxLength(200)
            .IsRequired();

        locationNodeEntity.Property(record => record.ParentLocationNodeId)
            .HasMaxLength(200);

        locationNodeEntity.Property(record => record.Name)
            .HasMaxLength(400);

        locationNodeEntity.Property(record => record.TemplateId)
            .HasMaxLength(200);

        locationNodeEntity.Property(record => record.LocationGroupId)
            .HasMaxLength(200);

        locationNodeEntity.Property(record => record.Aisle)
            .HasMaxLength(200);

        locationNodeEntity.Property(record => record.Shelf)
            .HasMaxLength(200);

        locationNodeEntity.Property(record => record.UpdatedAtUtc)
            .IsRequired();

        locationNodeEntity.HasIndex(record => record.ParentLocationNodeId);

        locationNodeEntity.HasIndex(record => record.LocationGroupId);

        locationNodeEntity.HasIndex(record => record.TemplateId);

        // (TenantId, LocationNodeId) must be unique per tenant.
        locationNodeEntity
            .HasIndex("TenantId", nameof(LocationNodeRecord.LocationNodeId))
            .HasDatabaseName("ix_location_nodes_tenant_location_node_id")
            .IsUnique();

        var templateDesignEntity = modelBuilder.Entity<TemplateDesignRecord>();
        templateDesignEntity.ToTable("template_designs");

        templateDesignEntity.HasKey(record => record.Id);

        templateDesignEntity.Property(record => record.TenantId)
            .HasMaxLength(200)
            .IsRequired();

        templateDesignEntity.Property(record => record.TemplateId)
            .HasMaxLength(200)
            .IsRequired();

        templateDesignEntity.Property(record => record.Name)
            .HasMaxLength(200)
            .IsRequired();

        templateDesignEntity.Property(record => record.Width)
            .IsRequired();

        templateDesignEntity.Property(record => record.Height)
            .IsRequired();

        templateDesignEntity.Property(record => record.BackgroundColor)
            .HasMaxLength(32)
            .IsRequired();

        templateDesignEntity.Property(record => record.ElementsJson)
            .IsRequired();

        templateDesignEntity.Property(record => record.DefaultsJson)
            .IsRequired();

        templateDesignEntity.Property(record => record.UpdatedAtUtc)
            .IsRequired();

        templateDesignEntity.HasIndex(record => new { record.TenantId, record.TemplateId })
            .IsUnique();

        var templateScopeSettingsEntity = modelBuilder.Entity<TemplateScopeSettingsRecord>();
        templateScopeSettingsEntity.ToTable("template_scope_settings");

        templateScopeSettingsEntity.HasKey(record => record.Id);

        templateScopeSettingsEntity.Property(record => record.TenantId)
            .HasMaxLength(200)
            .IsRequired();

        templateScopeSettingsEntity.Property(record => record.ScopeType)
            .HasMaxLength(32)
            .IsRequired();

        templateScopeSettingsEntity.Property(record => record.ScopeKey)
            .HasMaxLength(200)
            .IsRequired();

        templateScopeSettingsEntity.Property(record => record.SettingsJson)
            .IsRequired();

        templateScopeSettingsEntity.Property(record => record.UpdatedAtUtc)
            .IsRequired();

        templateScopeSettingsEntity.HasIndex(record => new { record.TenantId, record.ScopeType, record.ScopeKey })
            .IsUnique();

        var locationGroupEntity = modelBuilder.Entity<LocationGroupRecord>();
        locationGroupEntity.ToTable("location_groups");

        locationGroupEntity.HasKey(record => record.Id);

        locationGroupEntity.Property(record => record.TenantId)
            .HasMaxLength(200)
            .IsRequired();

        locationGroupEntity.Property(record => record.LocationGroupId)
            .HasMaxLength(200)
            .IsRequired();

        locationGroupEntity.Property(record => record.Name)
            .HasMaxLength(400)
            .IsRequired();

        locationGroupEntity.Property(record => record.UpdatedAtUtc)
            .IsRequired();

        locationGroupEntity.HasIndex(record => new { record.TenantId, record.LocationGroupId })
            .IsUnique();

        var templateAssetEntity = modelBuilder.Entity<TemplateAssetRecord>();
        templateAssetEntity.ToTable("template_assets");

        templateAssetEntity.HasKey(record => record.Id);

        templateAssetEntity.Property(record => record.TenantId)
            .HasMaxLength(200)
            .IsRequired();

        templateAssetEntity.Property(record => record.AssetId)
            .HasMaxLength(200)
            .IsRequired();

        templateAssetEntity.Property(record => record.AssetType)
            .HasMaxLength(50)
            .IsRequired();

        templateAssetEntity.Property(record => record.ObjectKey)
            .HasMaxLength(800)
            .IsRequired();

        templateAssetEntity.Property(record => record.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        templateAssetEntity.Property(record => record.OriginalFileName)
            .HasMaxLength(260)
            .IsRequired();

        templateAssetEntity.Property(record => record.ChecksumSha256)
            .HasMaxLength(128)
            .IsRequired();

        templateAssetEntity.Property(record => record.SizeBytes)
            .IsRequired();

        templateAssetEntity.Property(record => record.UpdatedAtUtc)
            .IsRequired();

        templateAssetEntity.HasIndex(record => new { record.TenantId, record.AssetId })
            .IsUnique();
    }
}

internal sealed class TemplateFontMetadataRecord
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public string TemplateId { get; set; } = string.Empty;

    public string FontKey { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string ObjectKey { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string ChecksumSha256 { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class DisplayModelRecord
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public string DisplayModelId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Width { get; set; }

    public int Height { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class LocationNodeRecord
{
    public Guid Id { get; set; }

    public string LocationNodeId { get; set; } = string.Empty;

    public string? ParentLocationNodeId { get; set; }

    public string? Name { get; set; }

    public string? TemplateId { get; set; }

    public string? LocationGroupId { get; set; }

    public string? Aisle { get; set; }

    public string? Shelf { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal static class DisplayModelSeedData
{
    internal const string SharedTenantId = "_shared";

    internal static readonly IReadOnlyList<DisplayModelSeedItem> SharedDefaults =
    [
        new("teck-4-2-inch", "4.2\" ESL", 1200, 825),
        new("teck-2-9-inch", "2.9\" ESL", 296, 128),
        new("teck-1-54-inch", "1.54\" ESL", 200, 200),
    ];
}

internal sealed record DisplayModelSeedItem(string DisplayModelId, string Name, int Width, int Height);
