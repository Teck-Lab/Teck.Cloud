// <copyright file="GetDisplayItemResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.Displays.Features.GetDisplays.V1;

/// <summary>
/// Response item for a single display.
/// </summary>
/// <param name="DisplayId">Primary key.</param>
/// <param name="ShortSerial">4-byte serial in XX-XX-XX-XX format.</param>
/// <param name="LongSerial">Decimal serial (null until first heartbeat).</param>
/// <param name="LocationNodeId">Assigned location node.</param>
/// <param name="DeviceDefinitionId">Optional device model.</param>
/// <param name="CreatedAt">Registration time (UTC).</param>
public sealed record GetDisplayItemResponse(
    Guid DisplayId,
    string ShortSerial,
    long? LongSerial,
    string LocationNodeId,
    Guid? DeviceDefinitionId,
    DateTimeOffset CreatedAt);
