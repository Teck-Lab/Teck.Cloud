// <copyright file="GetHealthEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,AV1555,AV1580,CA1515,CA1062,CS1591
using System.Diagnostics.CodeAnalysis;
using FastEndpoints;

namespace Web.Aggregate.Gateway.Endpoints.V1;

public sealed class GetHealthEndpoint : EndpointWithoutRequest<HealthResponse>
{
    public override void Configure()
    {
        Get("/health");
        Version(1);
        AllowAnonymous();
        Tags("Health");
    }

    [RequiresDynamicCode()]
    [RequiresUnreferencedCode()]
    public override async Task HandleAsync(CancellationToken ct)
    {
        HealthResponse response = new("ok");
        HttpContext.Response.StatusCode = StatusCodes.Status200OK;
        await HttpContext.Response.WriteAsJsonAsync(response, ct).ConfigureAwait(false);
    }
}
