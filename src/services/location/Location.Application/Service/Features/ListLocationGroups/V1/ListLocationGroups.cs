// <copyright file="ListLocationGroups.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using Location.Application.Service.Abstractions;
using SharedKernel.Core.CQRS;

namespace Location.Application.Service.Features.ListLocationGroups.V1;

/// <summary>
/// Query for listing location groups.
/// </summary>
public sealed record ListLocationGroupsQuery
    : IQuery<ErrorOr<ListLocationGroupsResponse>>;

/// <summary>
/// Handler for <see cref="ListLocationGroupsQuery"/>.
/// </summary>
/// <param name="readRepository">Location group read repository dependency.</param>
public sealed class ListLocationGroupsQueryHandler(ILocationGroupReadRepository readRepository)
    : IQueryHandler<ListLocationGroupsQuery, ErrorOr<ListLocationGroupsResponse>>
{
    private readonly ILocationGroupReadRepository readRepository = readRepository;

    /// <summary>
    /// Lists location groups and maps them to response items.
    /// </summary>
    /// <param name="request">The list location groups query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The location group list response.</returns>
    public async ValueTask<ErrorOr<ListLocationGroupsResponse>> Handle(
        ListLocationGroupsQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LocationGroupSnapshot> groups = await this.readRepository
            .ListByTenantAsync("_current", cancellationToken)
            .ConfigureAwait(false);

        return new ListLocationGroupsResponse
        {
            Groups = groups.Select(group => new LocationGroupListItem
            {
                LocationGroupId = group.LocationGroupId,
                Name = group.Name,
            }).ToList(),
        };
    }
}

/// <summary>
/// Response payload for location group listing.
/// </summary>
public sealed record ListLocationGroupsResponse
{
    /// <summary>Gets the location groups.</summary>
    public IReadOnlyList<LocationGroupListItem> Groups { get; init; } = [];
}

/// <summary>
/// Response item for a location group.
/// </summary>
public sealed record LocationGroupListItem
{
    /// <summary>Gets the location group identifier.</summary>
    public string LocationGroupId { get; init; } = string.Empty;

    /// <summary>Gets the location group name.</summary>
    public string Name { get; init; } = string.Empty;
}
