// <copyright file="UpsertTemplateScopeSettingsEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using ErrorOr;
using FastEndpoints;
using Location.Application.Service.Features.UpsertTemplateScopeSettings.V1;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Location.Api.Endpoints.V1.Service;

public sealed class UpsertTemplateScopeSettingsEndpoint(ISender sender)
    : Endpoint<UpsertTemplateScopeSettingsRequest, UpsertTemplateScopeSettingsResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Post("/Service/TemplateScopeSettings");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    public override async Task HandleAsync(UpsertTemplateScopeSettingsRequest request, CancellationToken ct)
    {
        UpsertTemplateScopeSettingsCommand command = new(request.ScopeType, request.ScopeKey, request.SettingsJson);
        ErrorOr<UpsertTemplateScopeSettingsResponse> commandResponse = await this.sender.Send(command, ct).ConfigureAwait(false);
        await this.SendAsync(commandResponse, cancellation: ct).ConfigureAwait(false);
    }
}
