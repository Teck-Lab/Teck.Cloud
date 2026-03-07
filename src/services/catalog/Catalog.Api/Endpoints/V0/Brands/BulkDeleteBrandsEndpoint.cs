// <copyright file="BulkDeleteBrandsEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Catalog.Application.Brands.Features.DeleteBrands.V1;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;

namespace Catalog.Api.Endpoints.V0.Brands;

public sealed class BulkDeleteBrandsEndpoint(ISender sender) : Endpoint<DeleteBrandsRequest, EmptyResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Post("/Brands/bulk/delete");
        Version(0);
        Options(endpoint => endpoint.RequireProtectedResource("brands", "update"));
        Summary(summary => summary.Summary = "Bulk delete brands");
    }

    public override async Task HandleAsync(DeleteBrandsRequest request, CancellationToken ct)
    {
        DeleteBrandsCommand command = new(request.Ids);
        var commandResponse = await sender.Send(command, ct).ConfigureAwait(false);
        await this.SendNoContentAsync(commandResponse, ct).ConfigureAwait(false);
    }
}
