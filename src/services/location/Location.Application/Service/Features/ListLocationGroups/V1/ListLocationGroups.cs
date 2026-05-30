// <copyright file="ListLocationGroups.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using Location.Application.Service.Abstractions;
using SharedKernel.Core.CQRS;

namespace Location.Application.Service.Features.ListLocationGroups.V1;

public sealed record ListLocationGroupsQuery
    : IQuery<ErrorOr<ListLocationGroupsResponse>>;

public sealed class ListLocationGroupsQueryHandler(ILocationGroupReadRepository readRepository)
    : IQueryHandler<ListLocationGroupsQuery, ErrorOr<ListLocationGroupsResponse>>
{
    private readonly ILocationGroupReadRepository readRepository = readRepository;

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

public sealed record ListLocationGroupsResponse
{
    public IReadOnlyList<LocationGroupListItem> Groups { get; init; } = [];
}

public sealed record LocationGroupListItem
{
    public string LocationGroupId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}
