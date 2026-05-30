// <copyright file="DisplayOperationCompleted.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using Wolverine.Persistence.Sagas;

namespace Device.Application.Operations.Saga;

/// <summary>
/// Signals that the active display operation completed successfully.
/// </summary>
/// <param name="DisplayId">The display identifier.</param>
/// <param name="OperationType">The operation type that completed.</param>
/// <param name="CompletedAt">The completion timestamp.</param>
/// <param name="ResultPayload">Optional operation result payload.</param>
[MemoryPackable]
public sealed partial record DisplayOperationCompleted(
    [property: SagaIdentity]
    Guid DisplayId,
    string OperationType,
    DateTimeOffset CompletedAt,
    string? ResultPayload);
