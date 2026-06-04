// <copyright file="UpsertLocationGroup.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using Location.Application.Service.Abstractions;
using SharedKernel.Core.CQRS;

namespace Location.Application.Service.Features.UpsertLocationGroup.V1;

/// <summary>
/// Command for creating or updating a location group.
/// </summary>
/// <param name="LocationGroupId">The location group identifier.</param>
/// <param name="Name">The location group name.</param>
public sealed record UpsertLocationGroupCommand(
    string LocationGroupId,
    string Name)
    : ICommand<ErrorOr<UpsertLocationGroupResponse>>;

/// <summary>
/// Handler for <see cref="UpsertLocationGroupCommand"/>.
/// </summary>
/// <param name="writeRepository">Location group write repository dependency.</param>
public sealed class UpsertLocationGroupCommandHandler(ILocationGroupWriteRepository writeRepository)
    : ICommandHandler<UpsertLocationGroupCommand, ErrorOr<UpsertLocationGroupResponse>>
{
    private readonly ILocationGroupWriteRepository writeRepository = writeRepository;

    /// <summary>
    /// Creates or updates a location group.
    /// </summary>
    /// <param name="request">The upsert location group command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The upsert location group response.</returns>
    public async ValueTask<ErrorOr<UpsertLocationGroupResponse>> Handle(
        UpsertLocationGroupCommand request,
        CancellationToken cancellationToken)
    {
        var snapshot = new LocationGroupSnapshot(
            TenantId: "_current",
            request.LocationGroupId,
            request.Name);

        await this.writeRepository.UpsertAsync(snapshot, cancellationToken).ConfigureAwait(false);

        return new UpsertLocationGroupResponse
        {
            LocationGroupId = request.LocationGroupId,
            Name = request.Name,
        };
    }
}

/// <summary>
/// Response payload for location group upsert.
/// </summary>
public sealed record UpsertLocationGroupResponse
{
    /// <summary>Gets the location group identifier.</summary>
    public string LocationGroupId { get; init; } = string.Empty;

    /// <summary>Gets the location group name.</summary>
    public string Name { get; init; } = string.Empty;
}
