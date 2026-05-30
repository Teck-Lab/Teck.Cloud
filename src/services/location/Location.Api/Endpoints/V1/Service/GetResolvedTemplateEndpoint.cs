// <copyright file="GetResolvedTemplateEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using ErrorOr;
using FastEndpoints;
using Location.Application.Service.Features.GetResolvedTemplate.V1;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Location.Api.Endpoints.V1.Service;

public sealed class GetResolvedTemplateEndpoint(ISender sender)
    : Endpoint<GetResolvedTemplateRequest, GetResolvedTemplateResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Post("/Service/ResolvedTemplate");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    public override async Task HandleAsync(GetResolvedTemplateRequest request, CancellationToken ct)
    {
        GetResolvedTemplateQuery query = new(request.LocationNodeId, request.ExplicitTemplateId);
        ErrorOr<GetResolvedTemplateResponse> queryResponse = await this.sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(queryResponse, cancellation: ct).ConfigureAwait(false);
    }
}
