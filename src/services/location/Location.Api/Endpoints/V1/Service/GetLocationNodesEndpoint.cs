// <copyright file="GetLocationNodesEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using ErrorOr;
using FastEndpoints;
using Location.Application.Service.Features.GetLocationNodes.V1;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Location.Api.Endpoints.V1.Service;

public sealed class GetLocationNodesEndpoint(ISender sender)
    : EndpointWithoutRequest<IReadOnlyList<GetLocationNodeItemResponse>>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Get("/Service/LocationNodes");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? query = Query<string>("q", isRequired: false);

        ErrorOr<IReadOnlyList<GetLocationNodeItemResponse>> result = await this.sender
            .Send(new GetLocationNodesQuery(query), ct)
            .ConfigureAwait(false);

        await this.SendAsync(result, cancellation: ct).ConfigureAwait(false);
    }
}
