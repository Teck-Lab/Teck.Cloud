// <copyright file="UpsertTemplateDesignEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062
using ErrorOr;
using FastEndpoints;
using Location.Application.Service.Features.UpsertTemplateDesign.V1;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Location.Api.Endpoints.V1.Service;

/// <summary>
/// Handles upsert template design requests.
/// </summary>
public sealed class UpsertTemplateDesignEndpoint(ISender sender)
    : Endpoint<UpsertTemplateDesignRequest, UpsertTemplateDesignResponse>
{
    private readonly ISender sender = sender;

    /// <summary>
    /// Configures the endpoint route, version, and access rules.
    /// </summary>
    public override void Configure()
    {
        Post("/Service/TemplateDesigns");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    /// <summary>
    /// Handles the incoming request and writes the HTTP response.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    public override async Task HandleAsync(UpsertTemplateDesignRequest request, CancellationToken ct)
    {
        UpsertTemplateDesignCommand command = new(
            request.TemplateId,
            request.Name,
            request.Width,
            request.Height,
            request.BackgroundColor,
            request.ElementsJson,
            request.DefaultsJson);

        ErrorOr<UpsertTemplateDesignResponse> commandResponse = await this.sender.Send(command, ct).ConfigureAwait(false);
        await this.SendAsync(commandResponse, cancellation: ct).ConfigureAwait(false);
    }
}
