// <copyright file="AddDisplaysEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Device.Application.Displays.Features.AddDisplays.V1;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Device.Api.Endpoints.V1.Displays;

public sealed class AddDisplaysEndpoint(ISender sender)
    : Endpoint<AddDisplaysRequest, AddDisplaysResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Post("/Displays/Batch");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("display", "create");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
    }

    public override async Task HandleAsync(AddDisplaysRequest request, CancellationToken ct)
    {
        IReadOnlyList<string> serials = request.Displays
            .Select(display => display.ShortSerial)
            .ToList();

        AddDisplaysCommand command = new(request.LocationNodeId, serials, request.DeviceDefinitionId);
        ErrorOr<AddDisplaysResponse> result = await this.sender.Send(command, ct).ConfigureAwait(false);
        await this.SendAsync(result, cancellation: ct).ConfigureAwait(false);
    }
}
