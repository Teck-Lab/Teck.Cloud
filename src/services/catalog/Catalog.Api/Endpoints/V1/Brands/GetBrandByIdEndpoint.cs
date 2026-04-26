// <copyright file="GetBrandByIdEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Catalog.Application.Brands.Features.GetBrandById.V1;
using ErrorOr;
using FastEndpoints;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Catalog.Api.Endpoints.V1.Brands;

public sealed class GetBrandByIdEndpoint(ISender sender) : Endpoint<GetBrandByIdRequest, GetByIdBrandResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Get("/Brands/{Id:guid}");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    public override async Task HandleAsync(GetBrandByIdRequest request, CancellationToken ct)
    {
        GetBrandByIdQuery query = new(request.Id);
        ErrorOr<GetByIdBrandResponse> queryResponse = await sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(queryResponse, cancellation: ct).ConfigureAwait(false);
    }
}
