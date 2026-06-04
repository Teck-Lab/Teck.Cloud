// <copyright file="CreateBrandEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062
using Catalog.Application.Brands.Features.CreateBrand.V1;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Catalog.Api.Endpoints.V1.Brands;

/// <summary>
/// Handles create brand requests.
/// </summary>
public sealed class CreateBrandEndpoint(ISender sender) : Endpoint<CreateBrandRequest, CreateBrandResponse>
{
    private readonly ISender sender = sender;

    /// <summary>
    /// Configures the endpoint route, version, and access rules.
    /// </summary>
    public override void Configure()
    {
        Post("/Brands");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("brand", "create");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
    }

    /// <summary>
    /// Handles the incoming request and writes the HTTP response.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    public override async Task HandleAsync(CreateBrandRequest request, CancellationToken ct)
    {
        CreateBrandCommand command = new(request.Name, request.Description, request.Website);
        var commandResponse = await sender.Send(command, ct).ConfigureAwait(false);

        await this
            .SendCreatedAsync(commandResponse, value => $"{HttpContext.Request.Path}/{value.Id}", ct)
            .ConfigureAwait(false);
    }
}
