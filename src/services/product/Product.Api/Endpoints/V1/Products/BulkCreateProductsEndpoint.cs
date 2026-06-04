// <copyright file="BulkCreateProductsEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using Product.Application.Product.Features.BulkCreateProducts.V1;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Product.Api.Endpoints.V1.Products;

/// <summary>
/// Bulk creates products from CSV text.
/// </summary>
public sealed class BulkCreateProductsEndpoint(ISender sender)
    : Endpoint<BulkCreateProductsCommand, BulkCreateProductsResponse>
{
    private readonly ISender sender = sender;

    /// <inheritdoc/>
    public override void Configure()
    {
        Post("/Products/Bulk");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("product", "create");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(BulkCreateProductsCommand request, CancellationToken ct)
    {
        var result = await this.sender.Send(request, ct).ConfigureAwait(false);
        await this.SendAsync(result, cancellation: ct).ConfigureAwait(false);
    }
}
