// <copyright file="DeviceWriteDbContext.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using Device.Domain.AccessPoints;
using Device.Domain.Entities.DeviceDefinitionAggregate;
using Device.Domain.Entities.DeviceLayoutAggregate;
using Device.Domain.Entities.DisplayAggregate;
using Device.Domain.Entities.DisplayAssignmentAggregate;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Infrastructure.MultiTenant;
using SharedKernel.Persistence.Database.EFCore;

namespace Device.Infrastructure.Persistence;

/// <summary>
/// EF Core persistence context for Device write-side infrastructure.
/// </summary>
public sealed class DeviceWriteDbContext : BaseDbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceWriteDbContext"/> class.
    /// </summary>
    /// <param name="options">The DbContext options.</param>
    /// <remarks>
    /// Single-argument constructor required by Wolverine's
    /// <c>TenantedDbContextBuilderByConnectionString</c>, which activates tenant-scoped
    /// contexts via <c>Activator.CreateInstance(typeof(T), options)</c>. The tenant
    /// identifier is recovered in <see cref="BaseDbContext"/> via
    /// <see cref="TenantDbContextOptionsExtension"/>.
    /// </remarks>
    [RequiresDynamicCode("Calls DbContext configuration which may require dynamic code at runtime.")]
    public DeviceWriteDbContext(DbContextOptions<DeviceWriteDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceWriteDbContext"/> class
    /// with a Finbuckle tenant accessor for HTTP-scoped resolution.
    /// </summary>
    /// <param name="options">The DbContext options.</param>
    /// <param name="tenantAccessor">The multi-tenant context accessor.</param>
    [RequiresDynamicCode("Calls DbContext configuration which may require dynamic code at runtime.")]
    public DeviceWriteDbContext(
        DbContextOptions<DeviceWriteDbContext> options,
        IMultiTenantContextAccessor<TenantDetails> tenantAccessor)
        : base(options, tenantAccessor: tenantAccessor)
    {
    }

    internal DbSet<DeviceDefinition> DeviceDefinitions => this.Set<DeviceDefinition>();

    internal DbSet<AccessPoint> AccessPoints => this.Set<AccessPoint>();

    internal DbSet<DeviceLayout> DeviceLayouts => this.Set<DeviceLayout>();

    internal DbSet<Display> Displays => this.Set<Display>();

    internal DbSet<DisplayAssignment> DisplayAssignments => this.Set<DisplayAssignment>();

    /// <inheritdoc/>
    [RequiresUnreferencedCode("Calls ApplyConfigurationsFromAssembly which uses reflection.")]
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        // Wolverine envelope storage mapping is contributed by WolverineModelCustomizer,
        // which is registered via AddDbContextWithWolverineManagedMultiTenancy. Calling
        // MapWolverineEnvelopeStorage here would add the "WolverineEnabled" annotation
        // a second time and throw on model build.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(DeviceWriteDbContext).Assembly,
            type => type.FullName?.Contains("Config.Write", StringComparison.Ordinal) ?? false);
    }
}
