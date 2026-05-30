// <copyright file="HanshowHeartbeatRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable CA1515

namespace Device.Application.Hanshow.Features.Heartbeat.V1;

/// <summary>
/// Incoming heartbeat payload from a Hanshow device.
/// </summary>
public sealed class HanshowHeartbeatRequest
{
    /// <summary>
    /// Gets or sets the 4-byte serial in XX-XX-XX-XX format.
    /// </summary>
    public string ShortSerial { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the decimal serial number reported by the device.
    /// </summary>
    public long LongSerial { get; set; }
}
