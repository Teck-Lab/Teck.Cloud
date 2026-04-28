// <copyright file="GetPaginatedTenantsRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable CA1515

using SharedKernel.Core.Pagination;

namespace Customer.Api.Endpoints.V1.Tenants.GetPaginatedTenants;

/// <summary>
/// Request model for paginated tenant listing.
/// </summary>
public sealed class GetPaginatedTenantsRequest : PaginationParameters
{
    /// <summary>
    /// Gets or sets an optional keyword filter for identifier or name.
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// Gets or sets an optional plan filter.
    /// </summary>
    public string? Plan { get; set; }

    /// <summary>
    /// Gets or sets an optional active state filter.
    /// </summary>
    public bool? IsActive { get; set; }
}

#pragma warning restore CA1515

