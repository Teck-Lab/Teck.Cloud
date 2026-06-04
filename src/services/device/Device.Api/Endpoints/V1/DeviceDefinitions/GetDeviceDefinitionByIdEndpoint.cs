// <copyright file="GetDeviceDefinitionByIdEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062
using Device.Application.DeviceDefinitions.Features.GetDeviceDefinitionById.V1;
using ErrorOr;
using FastEndpoints;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Device.Api.Endpoints.V1.DeviceDefinitions;

/// <summary>
/// Handles get device definition by id requests.
/// </summary>
public sealed class GetDeviceDefinitionByIdEndpoint(ISender sender)
    : Endpoint<GetDeviceDefinitionByIdRequest, GetDeviceDefinitionByIdResponse>
{
    private readonly ISender sender = sender;

    /// <summary>
    /// Configures the endpoint route, version, and access rules.
    /// </summary>
    public override void Configure()
    {
        Get("/DeviceDefinitions/{id}");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    /// <summary>
    /// Handles the incoming request and writes the HTTP response.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    public override async Task HandleAsync(GetDeviceDefinitionByIdRequest request, CancellationToken ct)
    {
        GetDeviceDefinitionByIdQuery query = new(request.Id);
        ErrorOr<GetDeviceDefinitionByIdResponse> result = await this.sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(result, cancellation: ct).ConfigureAwait(false);
    }
}
