// <copyright file="UpsertLocationGroupEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using ErrorOr;
using FastEndpoints;
using Location.Application.Service.Features.UpsertLocationGroup.V1;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Location.Api.Endpoints.V1.Service;

public sealed class UpsertLocationGroupEndpoint(ISender sender)
    : Endpoint<UpsertLocationGroupRequest, UpsertLocationGroupResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Post("/Service/LocationGroups");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    public override async Task HandleAsync(UpsertLocationGroupRequest request, CancellationToken ct)
    {
        UpsertLocationGroupCommand command = new(request.LocationGroupId, request.Name);
        ErrorOr<UpsertLocationGroupResponse> commandResponse = await this.sender.Send(command, ct).ConfigureAwait(false);
        await this.SendAsync(commandResponse, cancellation: ct).ConfigureAwait(false);
    }
}
