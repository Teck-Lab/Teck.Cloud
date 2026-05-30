// <copyright file="DisplayOperationTimeout.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using Wolverine;
using Wolverine.Persistence.Sagas;

namespace Device.Application.Operations.Saga;

/// <summary>
/// Timeout marker for an in-flight display operation.
/// </summary>
/// <param name="DisplayId">The display identifier.</param>
[MemoryPackable]
public sealed partial record DisplayOperationTimeout([property: SagaIdentity] Guid DisplayId)
    : TimeoutMessage(TimeSpan.FromMinutes(20));
