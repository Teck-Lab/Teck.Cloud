// <copyright file="IHanshowHeartbeatProcessor.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.Hanshow.Abstractions;

/// <summary>
/// Input from a Hanshow device heartbeat request.
/// </summary>
/// <param name="ShortSerial">4-byte serial from the heartbeat packet (XX-XX-XX-XX).</param>
/// <param name="LongSerial">Decimal serial reported by the device.</param>
public sealed record HanshowHeartbeatData(string ShortSerial, long LongSerial);

/// <summary>
/// Processes incoming Hanshow ESL heartbeat signals.
/// </summary>
public interface IHanshowHeartbeatProcessor
{
    /// <summary>
    /// Handles a heartbeat from a Hanshow device.
    /// </summary>
    /// <param name="data">Heartbeat data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask ProcessAsync(HanshowHeartbeatData data, CancellationToken cancellationToken);
}
