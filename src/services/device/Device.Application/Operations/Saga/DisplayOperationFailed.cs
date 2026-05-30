// <copyright file="DisplayOperationFailed.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using Wolverine.Persistence.Sagas;

namespace Device.Application.Operations.Saga;

/// <summary>
/// Signals that the active display operation failed.
/// </summary>
/// <param name="DisplayId">The display identifier.</param>
/// <param name="OperationType">The operation type that failed.</param>
/// <param name="FailedAt">The failure timestamp.</param>
/// <param name="Reason">The failure reason.</param>
[MemoryPackable]
public sealed partial record DisplayOperationFailed(
    [property: SagaIdentity]
    Guid DisplayId,
    string OperationType,
    DateTimeOffset FailedAt,
    string Reason);
