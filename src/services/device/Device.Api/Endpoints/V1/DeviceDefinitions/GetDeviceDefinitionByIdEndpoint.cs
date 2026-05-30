// <copyright file="GetDeviceDefinitionByIdEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Device.Application.DeviceDefinitions.Features.GetDeviceDefinitionById.V1;
using ErrorOr;
using FastEndpoints;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Device.Api.Endpoints.V1.DeviceDefinitions;

public sealed class GetDeviceDefinitionByIdEndpoint(ISender sender)
    : Endpoint<GetDeviceDefinitionByIdRequest, GetDeviceDefinitionByIdResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Get("/DeviceDefinitions/{id}");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    public override async Task HandleAsync(GetDeviceDefinitionByIdRequest request, CancellationToken ct)
    {
        GetDeviceDefinitionByIdQuery query = new(request.Id);
        ErrorOr<GetDeviceDefinitionByIdResponse> result = await this.sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(result, cancellation: ct).ConfigureAwait(false);
    }
}
