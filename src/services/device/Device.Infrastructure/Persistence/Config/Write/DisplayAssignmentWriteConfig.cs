// <copyright file="DisplayAssignmentWriteConfig.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DisplayAssignmentAggregate;
using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Device.Infrastructure.Persistence.Config.Write;

internal sealed class DisplayAssignmentWriteConfig : IEntityTypeConfiguration<DisplayAssignment>
{
    public void Configure(EntityTypeBuilder<DisplayAssignment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("display_assignments");

        builder.HasKey(assignment => assignment.Id);

        builder.Property(assignment => assignment.DisplayId)
            .IsRequired();

        builder.Property(assignment => assignment.LocationNodeId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(assignment => assignment.ResolvedTemplateId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(assignment => assignment.TemplateSource)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(assignment => assignment.AssignmentVersion)
            .IsRequired();

        builder.Property(assignment => assignment.RenderJobId)
            .IsRequired();

        builder.Property(assignment => assignment.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(assignment => assignment.RenderedImageUri)
            .HasConversion(
                uri => uri == null ? null : uri.ToString(),
                value => value == null ? null : new Uri(value))
            .HasMaxLength(1024);

        builder.Property(assignment => assignment.RenderedAtUtc);

        builder.Property(assignment => assignment.DeliveredAtUtc);

        builder.Property(assignment => assignment.FailureReason)
            .HasMaxLength(500);

        builder.Property(assignment => assignment.TemplateSnapshot)
            .HasMaxLength(8192);

        builder.Property(assignment => assignment.ProductDataSnapshot)
            .HasMaxLength(8192);

        builder.HasIndex(assignment => assignment.DisplayId)
            .HasDatabaseName("ix_display_assignments_display_id");

        builder.HasIndex(assignment => assignment.RenderJobId)
            .HasDatabaseName("ix_display_assignments_render_job_id");

        builder.OwnsMany(
            assignment => assignment.Zones,
            zoneBuilder =>
            {
                zoneBuilder.ToTable("display_assignment_zones");
                zoneBuilder.WithOwner().HasForeignKey("AssignmentId");
                zoneBuilder.HasKey("AssignmentId", nameof(DisplayAssignmentZone.ZoneIndex));

                zoneBuilder.Property(zone => zone.ZoneIndex)
                    .IsRequired();

                zoneBuilder.Property(zone => zone.ProductId)
                    .IsRequired();
            });

        builder.Navigation(assignment => assignment.Zones)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.ConfigureAuditProperties();

        builder.IsMultiTenant();
    }
}
