// <copyright file="GetDeviceDefinitionsEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062
using Device.Application.DeviceDefinitions.Features.GetDeviceDefinitions.V1;
using ErrorOr;
using FastEndpoints;
using Mediator;
using SharedKernel.Core.Pagination;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Device.Api.Endpoints.V1.DeviceDefinitions;

/// <summary>
/// Handles get device definitions requests.
/// </summary>
public sealed class GetDeviceDefinitionsEndpoint(ISender sender)
    : Endpoint<GetDeviceDefinitionsRequest, PagedList<GetDeviceDefinitionItemResponse>>
{
    private readonly ISender sender = sender;

    /// <summary>
    /// Configures the endpoint route, version, and access rules.
    /// </summary>
    public override void Configure()
    {
        Get("/DeviceDefinitions");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    /// <summary>
    /// Handles the incoming request and writes the HTTP response.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    public override async Task HandleAsync(GetDeviceDefinitionsRequest request, CancellationToken ct)
    {
        GetDeviceDefinitionsQuery query = new(request.Page, request.Size, request.SortBy, request.SortDescending);
        ErrorOr<PagedList<GetDeviceDefinitionItemResponse>> result = await this.sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(result, cancellation: ct).ConfigureAwait(false);
    }
}
