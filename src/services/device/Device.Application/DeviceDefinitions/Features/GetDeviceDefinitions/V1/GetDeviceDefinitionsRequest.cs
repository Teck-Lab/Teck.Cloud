// <copyright file="GetDeviceDefinitionsRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Pagination;

namespace Device.Application.DeviceDefinitions.Features.GetDeviceDefinitions.V1;

/// <summary>
/// Request model for paginated device definition queries.
/// </summary>
public sealed class GetDeviceDefinitionsRequest : PaginationParameters
{
    /// <summary>
    /// Gets or sets the sort column. Allowed values: modelId, name, eslProvider.
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to sort descending.
    /// </summary>
    public bool SortDescending { get; set; }
}
