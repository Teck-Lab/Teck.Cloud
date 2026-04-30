// <copyright file="GetSupplierByIdEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Catalog.Application.Suppliers.Features.GetSupplierById.V1;
using ErrorOr;
using FastEndpoints;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Catalog.Api.Endpoints.V1.Suppliers;

public sealed class GetSupplierByIdEndpoint(ISender sender) : Endpoint<GetSupplierByIdRequest, GetByIdSupplierResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Get("/Suppliers/{Id:guid}");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    public override async Task HandleAsync(GetSupplierByIdRequest request, CancellationToken ct)
    {
        GetSupplierByIdQuery query = new(request.Id);
        ErrorOr<GetByIdSupplierResponse> queryResponse = await sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(queryResponse, cancellation: ct).ConfigureAwait(false);
    }
}
