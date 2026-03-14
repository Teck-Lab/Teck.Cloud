// <copyright file="GetProductByIdEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Catalog.Application.Features.Products.GetProductById.V1;
using Catalog.Application.Products.Features.GetProductById.V1;
using Catalog.Application.Products.Responses;
using ErrorOr;
using FastEndpoints;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;

namespace Catalog.Api.Endpoints.V0.Products;

public sealed class GetProductByIdEndpoint(ISender sender) : Endpoint<GetProductByIdRequest, ProductResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Get("/Products/{ProductId:guid}");
        Version(0);
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetProductByIdRequest request, CancellationToken ct)
    {
        GetProductByIdQuery query = new(request.ProductId);
        ErrorOr<ProductResponse> queryResponse = await sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(queryResponse, cancellation: ct).ConfigureAwait(false);
    }
}
