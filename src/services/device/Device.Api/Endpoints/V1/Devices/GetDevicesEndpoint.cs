// <copyright file="GetDevicesEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062
using Device.Application.Devices.Features.GetDevices.V1;
using ErrorOr;
using FastEndpoints;
using Mediator;
using SharedKernel.Core.Pagination;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Device.Api.Endpoints.V1.Devices;

/// <summary>
/// Handles get devices requests.
/// </summary>
public sealed class GetDevicesEndpoint(ISender sender)
    : Endpoint<GetDevicesRequest, PagedList<GetDeviceItemResponse>>
{
    private readonly ISender sender = sender;

    /// <summary>
    /// Configures the endpoint route, version, and access rules.
    /// </summary>
    public override void Configure()
    {
        Get("/Devices");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    /// <summary>
    /// Handles the incoming request and writes the HTTP response.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    public override async Task HandleAsync(GetDevicesRequest request, CancellationToken ct)
    {
        GetDevicesQuery query = new(request.Page, request.Size, request.SortBy, request.SortDescending);
        ErrorOr<PagedList<GetDeviceItemResponse>> result = await this.sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(result, cancellation: ct).ConfigureAwait(false);
    }
}
