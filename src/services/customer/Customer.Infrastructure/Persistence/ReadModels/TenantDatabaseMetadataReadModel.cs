// <copyright file="TenantDatabaseMetadataReadModel.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read model for tenant database metadata rows.
/// </summary>
public sealed class TenantDatabaseMetadataReadModel
{
    /// <summary>
    /// Gets or sets the tenant ID.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Gets or sets the service name.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the read database mode.
    /// </summary>
    public int ReadDatabaseMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this row is soft deleted.
    /// </summary>
    public bool IsDeleted { get; set; }
}
