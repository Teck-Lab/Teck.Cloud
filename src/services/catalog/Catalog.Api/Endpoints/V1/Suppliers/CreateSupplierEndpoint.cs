// <copyright file="CreateSupplierEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Catalog.Application.Suppliers.Features.CreateSupplier.V1;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Catalog.Api.Endpoints.V1.Suppliers;

public sealed class CreateSupplierEndpoint(ISender sender) : Endpoint<CreateSupplierRequest, CreateSupplierResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Post("/Suppliers");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("supplier", "create");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
        Validator<CreateSupplierValidator>();
    }

    public override async Task HandleAsync(CreateSupplierRequest request, CancellationToken ct)
    {
        CreateSupplierCommand command = new(request.Name, request.Description, request.Website);
        var commandResponse = await sender.Send(command, ct).ConfigureAwait(false);

        await this
            .SendCreatedAsync(commandResponse, value => $"{HttpContext.Request.Path}/{value.Id}", ct)
            .ConfigureAwait(false);
    }
}
