// <copyright file="CreateDeviceLayoutEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Device.Application.DeviceLayouts.Features.CreateDeviceLayout.V1;
using ErrorOr;
using FastEndpoints;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Device.Api.Endpoints.V1.DeviceLayouts;

public sealed class CreateDeviceLayoutEndpoint(ISender sender)
    : Endpoint<CreateDeviceLayoutRequest, CreateDeviceLayoutResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Post("/DeviceLayouts");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    public override async Task HandleAsync(CreateDeviceLayoutRequest request, CancellationToken ct)
    {
        CreateDeviceLayoutCommand command = new(request.DeviceDefinitionId, request.Name, request.MaxZoneCount);
        ErrorOr<CreateDeviceLayoutResponse> result = await this.sender.Send(command, ct).ConfigureAwait(false);
        await this.SendAsync(result, cancellation: ct).ConfigureAwait(false);
    }
}
