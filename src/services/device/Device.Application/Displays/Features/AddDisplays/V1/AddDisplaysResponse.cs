// <copyright file="AddDisplaysResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.Displays.Features.AddDisplays.V1;

/// <summary>
/// Result for a single display in the batch add response.
/// </summary>
/// <param name="ShortSerial">The short serial that was processed.</param>
/// <param name="DisplayId">The created display ID (null if duplicate/failed).</param>
/// <param name="Duplicate">Whether the serial was already registered.</param>
public sealed record AddDisplayResult(
    string ShortSerial,
    Guid? DisplayId,
    bool Duplicate);

/// <summary>
/// Response from a batch add displays operation.
/// </summary>
/// <param name="Results">Per-display outcomes.</param>
/// <param name="AddedCount">Number of newly registered displays.</param>
/// <param name="DuplicateCount">Number of serials that were already registered.</param>
public sealed record AddDisplaysResponse(
    IReadOnlyList<AddDisplayResult> Results,
    int AddedCount,
    int DuplicateCount);
