// <copyright file="ListTemplateDesignsEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using ErrorOr;
using FastEndpoints;
using Location.Application.Service.Features.ListTemplateDesigns.V1;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Location.Api.Endpoints.V1.Service;

public sealed class ListTemplateDesignsEndpoint(ISender sender)
    : EndpointWithoutRequest<ListTemplateDesignsResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Get("/Service/TemplateDesigns");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        ListTemplateDesignsQuery query = new();
        ErrorOr<ListTemplateDesignsResponse> queryResponse = await this.sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(queryResponse, cancellation: ct).ConfigureAwait(false);
    }
}
