// <copyright file="UpdateBrandEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Catalog.Application.Brands.Features.UpdateBrand.V1;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;

namespace Catalog.Api.Endpoints.V1.Brands;

public sealed class UpdateBrandEndpoint(ISender sender) : Endpoint<UpdateBrandRequest, EmptyResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Put("/Brands");
        Version(1);
        Options(endpoint => endpoint.RequireProtectedResource("brand", "update"));
    }

    public override async Task HandleAsync(UpdateBrandRequest request, CancellationToken ct)
    {
        UpdateBrandCommand command = new(request.Id, request.Name, request.Description, request.Website);
        var commandResponse = await sender.Send(command, ct).ConfigureAwait(false);
        await this.SendNoContentAsync(commandResponse, ct).ConfigureAwait(false);
    }
}
