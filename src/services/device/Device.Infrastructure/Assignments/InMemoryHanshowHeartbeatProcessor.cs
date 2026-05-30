// <copyright file="InMemoryHanshowHeartbeatProcessor.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Hanshow.Abstractions;

namespace Device.Infrastructure.Assignments;

/// <summary>
/// Stub implementation of <see cref="IHanshowHeartbeatProcessor"/> that logs heartbeats.
/// Replace with real Hanshow AP bridge integration when available.
/// </summary>
internal sealed class InMemoryHanshowHeartbeatProcessor(
    ILogger<InMemoryHanshowHeartbeatProcessor> logger) : IHanshowHeartbeatProcessor
{
    private readonly ILogger<InMemoryHanshowHeartbeatProcessor> logger = logger;

    /// <inheritdoc/>
    public ValueTask ProcessAsync(HanshowHeartbeatData data, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (this.logger.IsEnabled(LogLevel.Information))
        {
            this.logger.LogInformation(
                "Hanshow heartbeat received: ShortSerial={ShortSerial} LongSerial={LongSerial}",
                data.ShortSerial,
                data.LongSerial);
        }

        return ValueTask.CompletedTask;
    }
}
