// <copyright file="StartDisplayOperation.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using Wolverine.Persistence.Sagas;

namespace Device.Application.Operations.Saga;

/// <summary>
/// Requests sequential execution of an operation targeting a display.
/// </summary>
/// <param name="DisplayId">The display identifier.</param>
/// <param name="LocationNodeId">The display location node identifier.</param>
/// <param name="TenantId">The tenant identifier carried with the operation.</param>
/// <param name="OperationType">The operation type, such as Assign, Flip, or FlashLed.</param>
/// <param name="PayloadJson">Operation-specific serialized payload.</param>
/// <param name="RequestedAt">The timestamp when the operation was requested.</param>
[MemoryPackable]
public sealed partial record StartDisplayOperation(
    [property: SagaIdentity]
    Guid DisplayId,
    string LocationNodeId,
    string TenantId,
    string OperationType,
    string PayloadJson,
    DateTimeOffset RequestedAt);
