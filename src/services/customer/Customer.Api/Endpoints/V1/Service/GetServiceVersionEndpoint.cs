// <copyright file="GetServiceVersionEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,AV1555,AV1580,CA1515,CA1062,CS1591
using System.Reflection;
using FastEndpoints;

namespace Customer.Api.Endpoints.V1.Service;

public sealed class GetServiceVersionEndpoint : EndpointWithoutRequest<CustomerServiceVersionResponse>
{
    public override void Configure()
    {
        Get("/Service/Version");
        Version(1);
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string version = ResolveVersion();
        CustomerServiceVersionResponse response = new("customer", version);
        HttpContext.Response.StatusCode = StatusCodes.Status200OK;
        await HttpContext.Response.WriteAsJsonAsync(response, ct).ConfigureAwait(false);
    }

    private static string ResolveVersion()
    {
        Assembly assembly = typeof(GetServiceVersionEndpoint).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }
}
