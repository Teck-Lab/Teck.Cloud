// <copyright file="GetServiceVersionEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,AV1555,AV1580,CA1515,CA1062,CS1591
using FastEndpoints;

namespace Catalog.Api.Endpoints.V1.Service;

public sealed class GetServiceVersionEndpoint : EndpointWithoutRequest<CatalogServiceVersionResponse>
{
    public override void Configure()
    {
        this.Get("/Service/Version");
        this.Version(1);
        this.AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.StatusCode = StatusCodes.Status200OK;
        await HttpContext.Response
            .WriteAsJsonAsync(new CatalogServiceVersionResponse("catalog", CatalogVersionResolver.ResolveVersion()), cancellationToken: ct)
            .ConfigureAwait(false);
    }
}
