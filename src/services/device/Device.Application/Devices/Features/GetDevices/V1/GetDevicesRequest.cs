// <copyright file="GetDevicesRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Pagination;

namespace Device.Application.Devices.Features.GetDevices.V1;

/// <summary>
/// Request model for paginated device queries.
/// </summary>
public sealed class GetDevicesRequest : PaginationParameters
{
    /// <summary>
    /// Gets or sets the sort column. Allowed values: deviceId, maxZoneCount, updatedAtUtc.
    /// Defaults to deviceId when omitted.
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to sort descending.
    /// </summary>
    public bool SortDescending { get; set; }
}
