// <copyright file="GetDeviceLayoutsEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Device.Application.DeviceLayouts.Features.GetDeviceLayouts.V1;
using ErrorOr;
using FastEndpoints;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Device.Api.Endpoints.V1.DeviceLayouts;

public sealed class GetDeviceLayoutsEndpoint(ISender sender)
    : Endpoint<GetDeviceLayoutsRequest, IReadOnlyList<GetDeviceLayoutItemResponse>>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Get("/DeviceLayouts");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    public override async Task HandleAsync(GetDeviceLayoutsRequest request, CancellationToken ct)
    {
        GetDeviceLayoutsQuery query = new(request.DeviceDefinitionId);
        ErrorOr<IReadOnlyList<GetDeviceLayoutItemResponse>> result = await this.sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(result, cancellation: ct).ConfigureAwait(false);
    }
}
