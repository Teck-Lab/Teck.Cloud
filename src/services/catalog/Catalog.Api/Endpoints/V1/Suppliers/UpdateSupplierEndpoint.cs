// <copyright file="UpdateSupplierEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062
using Catalog.Application.Suppliers.Features.UpdateSupplier.V1;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Catalog.Api.Endpoints.V1.Suppliers;

/// <summary>
/// Handles update supplier requests.
/// </summary>
public sealed class UpdateSupplierEndpoint(ISender sender) : Endpoint<UpdateSupplierRequest, EmptyResponse>
{
    private readonly ISender sender = sender;

    /// <summary>
    /// Configures the endpoint route, version, and access rules.
    /// </summary>
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

    /// <summary>
    /// Handles the incoming request and writes the HTTP response.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    public override async Task HandleAsync(UpdateSupplierRequest request, CancellationToken ct)
    {
        UpdateSupplierCommand command = new(request.Id, request.Name, request.Description, request.Website);
        var commandResponse = await sender.Send(command, ct).ConfigureAwait(false);
        await this.SendNoContentAsync(commandResponse, ct).ConfigureAwait(false);
    }
}
