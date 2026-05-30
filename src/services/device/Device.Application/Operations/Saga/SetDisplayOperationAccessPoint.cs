// <copyright file="SetDisplayOperationAccessPoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using Wolverine.Persistence.Sagas;

namespace Device.Application.Operations.Saga;

/// <summary>
/// Stores the reserved access point serial number in display operation saga state.
/// </summary>
/// <param name="DisplayId">The display identifier.</param>
/// <param name="AccessPointSerial">The reserved access point serial number.</param>
[MemoryPackable]
public sealed partial record SetDisplayOperationAccessPoint(
    [property: SagaIdentity]
    Guid DisplayId,
    string AccessPointSerial);
