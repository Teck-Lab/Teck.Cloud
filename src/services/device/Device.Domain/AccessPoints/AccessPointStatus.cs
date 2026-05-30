// <copyright file="AccessPointStatus.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Domain.AccessPoints;

/// <summary>
/// Operational status for an ESL access point.
/// </summary>
public enum AccessPointStatus
{
    /// <summary>
    /// The access point is online and can accept display traffic.
    /// </summary>
    Online = 0,

    /// <summary>
    /// The access point is offline and cannot accept display traffic.
    /// </summary>
    Offline = 1,

    /// <summary>
    /// The access point is in maintenance mode and should not receive new assignments.
    /// </summary>
    Maintenance = 2,
}
