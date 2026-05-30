// <copyright file="UsageCounts.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Domain.Entities.LicenseAggregate;

/// <summary>
/// Represents current usage counts for quota validation.
/// </summary>
public sealed class UsageCounts
{
    /// <summary>
    /// Gets the current number of access points.
    /// </summary>
    public int AccessPoints { get; init; }

    /// <summary>
    /// Gets the current number of devices.
    /// </summary>
    public int Devices { get; init; }

    /// <summary>
    /// Gets the current number of products.
    /// </summary>
    public int Products { get; init; }

    /// <summary>
    /// Gets the current number of locations.
    /// </summary>
    public int Locations { get; init; }
}
