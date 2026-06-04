// <copyright file="UpdateBrandEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062
using Catalog.Application.Brands.Features.UpdateBrand.V1;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Catalog.Api.Endpoints.V1.Brands;

/// <summary>
/// Handles update brand requests.
/// </summary>
public sealed class UpdateBrandEndpoint(ISender sender) : Endpoint<UpdateBrandRequest, EmptyResponse>
{
    private readonly ISender sender = sender;

    /// <summary>
    /// Configures the endpoint route, version, and access rules.
    /// </summary>
    public override void Configure()
    {
        Put("/Brands");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("brand", "update");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("admin"));
        });
    }

    /// <summary>
    /// Handles the incoming request and writes the HTTP response.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    public override async Task HandleAsync(UpdateBrandRequest request, CancellationToken ct)
    {
        UpdateBrandCommand command = new(request.Id, request.Name, request.Description, request.Website);
        var commandResponse = await sender.Send(command, ct).ConfigureAwait(false);
        await this.SendNoContentAsync(commandResponse, ct).ConfigureAwait(false);
    }
}
