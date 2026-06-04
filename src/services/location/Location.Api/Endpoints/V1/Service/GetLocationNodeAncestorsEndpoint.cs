// <copyright file="GetLocationNodeAncestorsEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062
using ErrorOr;
using FastEndpoints;
using Location.Application.Service.Features.GetLocationNodeAncestors.V1;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Location.Api.Endpoints.V1.Service;

/// <summary>
/// Handles get location node ancestors requests.
/// </summary>
public sealed class GetLocationNodeAncestorsEndpoint(ISender sender)
    : EndpointWithoutRequest<IReadOnlyList<string>>
{
    private readonly ISender sender = sender;

    /// <summary>
    /// Configures the endpoint route, version, and access rules.
    /// </summary>
    public override void Configure()
    {
        Get("/Service/LocationNodes/{locationNodeId}/Ancestors");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    /// <summary>
    /// Handles the incoming request and writes the HTTP response.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    public override async Task HandleAsync(CancellationToken ct)
    {
        string locationNodeId = Route<string>("locationNodeId")!;

        ErrorOr<IReadOnlyList<string>> result = await this.sender
            .Send(new GetLocationNodeAncestorsQuery(locationNodeId), ct)
            .ConfigureAwait(false);

        await this.SendAsync(result, cancellation: ct).ConfigureAwait(false);
    }
}
