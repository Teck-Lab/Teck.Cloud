// <copyright file="UpdateSupplierEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Catalog.Application.Suppliers.Features.UpdateSupplier.V1;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Catalog.Api.Endpoints.V1.Suppliers;

public sealed class UpdateSupplierEndpoint(ISender sender) : Endpoint<UpdateSupplierRequest, EmptyResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Put("/Suppliers");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("supplier", "update");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("admin"));
        });
        Validator<UpdateSupplierValidator>();
    }

    public override async Task HandleAsync(UpdateSupplierRequest request, CancellationToken ct)
    {
        UpdateSupplierCommand command = new(request.Id, request.Name, request.Description, request.Website);
        var commandResponse = await sender.Send(command, ct).ConfigureAwait(false);
        await this.SendNoContentAsync(commandResponse, ct).ConfigureAwait(false);
    }
}
