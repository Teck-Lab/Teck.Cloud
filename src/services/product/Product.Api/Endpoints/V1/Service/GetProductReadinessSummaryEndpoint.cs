// <copyright file="GetProductReadinessSummaryEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062
using ErrorOr;
using FastEndpoints;
using Mediator;
using Product.Application.Service.Features.GetProductReadinessSummary.V1;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Product.Api.Endpoints.V1.Service;

/// <summary>
/// Handles get product readiness summary requests.
/// </summary>
public sealed class GetProductReadinessSummaryEndpoint(ISender sender)
    : Endpoint<GetProductReadinessSummaryRequest, GetProductReadinessSummaryResponse>
{
    private readonly ISender sender = sender;

    /// <summary>
    /// Configures the endpoint route, version, and access rules.
    /// </summary>
    public override void Configure()
    {
        Get("/Service/Readiness/Summary");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    /// <summary>
    /// Handles the incoming request and writes the HTTP response.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    public override async Task HandleAsync(GetProductReadinessSummaryRequest request, CancellationToken ct)
    {
        GetProductReadinessSummaryQuery query = new();
        ErrorOr<GetProductReadinessSummaryResponse> queryResponse = await this.sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(queryResponse, cancellation: ct).ConfigureAwait(false);
    }
}
