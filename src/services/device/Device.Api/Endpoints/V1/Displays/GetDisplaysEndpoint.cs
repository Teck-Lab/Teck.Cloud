// <copyright file="GetDisplaysEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Device.Application.Displays.Features.GetDisplays.V1;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Device.Api.Endpoints.V1.Displays;

public sealed class GetDisplaysEndpoint(ISender sender)
    : Endpoint<GetDisplaysRequest, IReadOnlyList<GetDisplayItemResponse>>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Get("/Displays");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("display", "list");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
    }

    public override async Task HandleAsync(GetDisplaysRequest request, CancellationToken ct)
    {
        GetDisplaysQuery query = new(request.LocationNodeId);
        ErrorOr<IReadOnlyList<GetDisplayItemResponse>> result = await this.sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(result, cancellation: ct).ConfigureAwait(false);
    }
}
