// <copyright file="ReadDatabaseMode.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Domain.Entities.TenantAggregate;

/// <summary>
/// Read database configuration mode for a tenant service.
/// </summary>
public enum ReadDatabaseMode
{
    /// <summary>
    /// Reads and writes use the same database.
    /// </summary>
    SharedWrite = 0,

    /// <summary>
    /// Reads use a dedicated read database.
    /// </summary>
    SeparateRead = 1,
}
