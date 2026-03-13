// <copyright file="GetServiceVersion2Endpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,AV1555,AV1580,CA1515,CA1062,CS1591
using System.Diagnostics.CodeAnalysis;
using FastEndpoints;

namespace Catalog.Api.Endpoints.V1.Service;

public sealed class GetServiceVersion2Endpoint : EndpointWithoutRequest<CatalogServiceVersionResponse>
{
    public override void Configure()
    {
        this.Get("/Service/Version2");
        this.Version(1);
        this.AllowAnonymous();
    }

    [RequiresDynamicCode()]
    [RequiresUnreferencedCode()]
    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.StatusCode = StatusCodes.Status200OK;
        await HttpContext.Response
            .WriteAsJsonAsync(new CatalogServiceVersionResponse("catalog-v2", CatalogVersionResolver.ResolveVersion()), cancellationToken: ct)
            .ConfigureAwait(false);
    }
}
