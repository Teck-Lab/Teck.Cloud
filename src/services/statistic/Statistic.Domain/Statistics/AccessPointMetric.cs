// <copyright file="AccessPointMetric.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Statistic.Domain.Statistics;

/// <summary>
/// Access point metrics captured for dashboard statistics.
/// </summary>
/// <param name="SerialNumber">Access point serial number.</param>
/// <param name="Vendor">Access point vendor.</param>
/// <param name="LocationNodeId">Location node identifier where the access point is deployed.</param>
/// <param name="Status">Current operational status.</param>
/// <param name="CurrentLoad">Current connected load value.</param>
/// <param name="MaxCapacity">Maximum supported load capacity.</param>
public sealed record AccessPointMetric(
    string SerialNumber,
    string Vendor,
    string LocationNodeId,
    string Status,
    int CurrentLoad,
    int MaxCapacity);
