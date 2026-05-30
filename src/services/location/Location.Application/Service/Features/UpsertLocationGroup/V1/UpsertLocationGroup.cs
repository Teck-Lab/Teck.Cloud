// <copyright file="UpsertLocationGroup.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using Location.Application.Service.Abstractions;
using SharedKernel.Core.CQRS;

namespace Location.Application.Service.Features.UpsertLocationGroup.V1;

public sealed record UpsertLocationGroupCommand(
    string LocationGroupId,
    string Name)
    : ICommand<ErrorOr<UpsertLocationGroupResponse>>;

public sealed class UpsertLocationGroupCommandHandler(ILocationGroupWriteRepository writeRepository)
    : ICommandHandler<UpsertLocationGroupCommand, ErrorOr<UpsertLocationGroupResponse>>
{
    private readonly ILocationGroupWriteRepository writeRepository = writeRepository;

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

public sealed record UpsertLocationGroupResponse
{
    public string LocationGroupId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}
