// <copyright file="DeleteBrandEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591,CA1034
using Catalog.Application.Brands.Features.DeleteBrand.V1;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Catalog.Api.Endpoints.V1.Brands;

public sealed class DeleteBrandEndpoint(ISender sender) : Endpoint<DeleteBrandEndpoint.DeleteBrandRouteRequest, EmptyResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Delete("/Brands/{Id:guid}");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("brand", "delete");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
    }

    public override async Task HandleAsync(DeleteBrandRouteRequest request, CancellationToken ct)
    {
        DeleteBrandCommand command = new(request.Id);
        var commandResponse = await sender.Send(command, ct).ConfigureAwait(false);
        await this.SendNoContentAsync(commandResponse, ct).ConfigureAwait(false);
    }

    public sealed record DeleteBrandRouteRequest(Guid Id);
}
