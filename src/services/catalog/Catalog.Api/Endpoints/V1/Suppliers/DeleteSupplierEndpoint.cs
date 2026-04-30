// <copyright file="DeleteSupplierEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591,CA1034
using Catalog.Application.Suppliers.Features.DeleteSupplier.V1;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Catalog.Api.Endpoints.V1.Suppliers;

public sealed class DeleteSupplierEndpoint(ISender sender) : Endpoint<DeleteSupplierEndpoint.DeleteSupplierRoute, EmptyResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Delete("/Suppliers/{Id:guid}");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("supplier", "delete");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
    }

    public override async Task HandleAsync(DeleteSupplierRoute request, CancellationToken ct)
    {
        DeleteSupplierCommand command = new(request.Id);
        var commandResponse = await sender.Send(command, ct).ConfigureAwait(false);
        await this.SendNoContentAsync(commandResponse, ct).ConfigureAwait(false);
    }

    public sealed record DeleteSupplierRoute(Guid Id);
}
