// <copyright file="DeviceReadDbContext.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using Device.Application.DeviceDefinitions.ReadModels;
using Device.Application.DeviceLayouts.ReadModels;
using Device.Domain.AccessPoints;
using Device.Domain.Entities.DisplayAggregate;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Infrastructure.MultiTenant;
using SharedKernel.Persistence.Database.EFCore;

namespace Device.Infrastructure.Persistence;

/// <summary>
/// EF Core read context for Device read-side infrastructure.
/// </summary>
public sealed class DeviceReadDbContext(
    DbContextOptions<DeviceReadDbContext> options,
    IMultiTenantContextAccessor<TenantDetails>? tenantAccessor = null)
    : BaseDbContext(options, tenantAccessor: tenantAccessor)
{
    internal DbSet<DeviceDefinitionReadModel> DeviceDefinitions => this.Set<DeviceDefinitionReadModel>();

    internal DbSet<AccessPoint> AccessPoints => this.Set<AccessPoint>();

    internal DbSet<DeviceLayoutReadModel> DeviceLayouts => this.Set<DeviceLayoutReadModel>();

    internal DbSet<Display> Displays => this.Set<Display>();

    /// <inheritdoc/>
    [RequiresUnreferencedCode("Calls ApplyConfigurationsFromAssembly which uses reflection.")]
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(DeviceReadDbContext).Assembly,
            type => type.FullName?.Contains("Config.Read", StringComparison.Ordinal) ?? false);
    }
}
