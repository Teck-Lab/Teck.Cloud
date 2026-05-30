// <copyright file="CreateProductEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using Product.Application.Product.Features.CreateProduct.V1;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Product.Api.Endpoints.V1.Products;

/// <summary>
/// Creates a new product.
/// </summary>
public sealed class CreateProductEndpoint(ISender sender)
    : Endpoint<CreateProductCommand, CreateProductResponse>
{
    private readonly ISender sender = sender;

    /// <inheritdoc/>
    public override void Configure()
    {
        Post("/Products");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("product", "create");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CreateProductCommand request, CancellationToken ct)
    {
        var result = await this.sender.Send(request, ct).ConfigureAwait(false);
        await this.SendAsync(result, cancellation: ct).ConfigureAwait(false);
    }
}
