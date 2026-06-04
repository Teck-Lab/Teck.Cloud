// <copyright file="DeleteSupplierEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CA1034
using Catalog.Application.Suppliers.Features.DeleteSupplier.V1;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Catalog.Api.Endpoints.V1.Suppliers;

/// <summary>
/// Handles delete supplier requests.
/// </summary>
public sealed class DeleteSupplierEndpoint(ISender sender) : Endpoint<DeleteSupplierEndpoint.DeleteSupplierRoute, EmptyResponse>
{
    private readonly ISender sender = sender;

    /// <summary>
    /// Configures the endpoint route, version, and access rules.
    /// </summary>
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

    /// <summary>
    /// Handles the incoming request and writes the HTTP response.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    public override async Task HandleAsync(DeleteSupplierRoute request, CancellationToken ct)
    {
        DeleteSupplierCommand command = new(request.Id);
        var commandResponse = await sender.Send(command, ct).ConfigureAwait(false);
        await this.SendNoContentAsync(commandResponse, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Represents delete supplier data.
    /// </summary>
    public sealed record DeleteSupplierRoute(Guid Id);
}
