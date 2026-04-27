// <copyright file="WolverineTenantConnectionSource.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using JasperFx.Descriptors;
using JasperFx.MultiTenancy;

namespace SharedKernel.Persistence.Database.MultiTenant;

/// <summary>
/// Dynamic tenant source used by Wolverine for per-tenant message storage connections.
/// </summary>
public sealed class WolverineTenantConnectionSource : IDynamicTenantSource<string>
{
    private readonly ConcurrentDictionary<string, string> activeTenants = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> disabledTenants = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the default shared-database write connection string.
    /// </summary>
    public string DefaultWriteConnectionString { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WolverineTenantConnectionSource"/> class.
    /// </summary>
    /// <param name="defaultWriteConnectionString">The default shared database connection string.</param>
    public WolverineTenantConnectionSource(string defaultWriteConnectionString)
    {
        this.DefaultWriteConnectionString = defaultWriteConnectionString;
    }

    /// <inheritdoc/>
    public Task AddTenantAsync(string tenantId, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        this.activeTenants[tenantId] = value;
        this.disabledTenants.TryRemove(tenantId, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DisableTenantAsync(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        if (this.activeTenants.TryRemove(tenantId, out string? value))
        {
            this.disabledTenants[tenantId] = value;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveTenantAsync(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        this.activeTenants.TryRemove(tenantId, out _);
        this.disabledTenants.TryRemove(tenantId, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> AllDisabledAsync()
    {
        IReadOnlyList<string> disabled = this.disabledTenants.Keys.ToList();
        return Task.FromResult(disabled);
    }

    /// <inheritdoc/>
    public Task EnableTenantAsync(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        if (this.disabledTenants.TryRemove(tenantId, out string? value))
        {
            this.activeTenants[tenantId] = value;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public DatabaseCardinality Cardinality => DatabaseCardinality.DynamicMultiple;

    /// <inheritdoc/>
    public ValueTask<string> FindAsync(string tenantId)
    {
        if (this.activeTenants.TryGetValue(tenantId, out string? value))
        {
            return ValueTask.FromResult(value);
        }

        return ValueTask.FromResult(this.DefaultWriteConnectionString);
    }

    /// <inheritdoc/>
    public Task RefreshAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> AllActive()
    {
        return this.activeTenants.Values
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<Assignment<string>> AllActiveByTenant()
    {
        return this.activeTenants
            .Select(pair => new Assignment<string>(pair.Key, pair.Value))
            .ToList();
    }
}
