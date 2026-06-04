// <copyright file="HanshowHeartbeatEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062
using Device.Application.Hanshow.Abstractions;
using Device.Application.Hanshow.Features.Heartbeat.V1;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using SharedKernel.Infrastructure.OpenApi;

namespace Device.Api.Endpoints.V1.Hanshow;

/// <summary>
/// Handles hanshow heartbeat requests.
/// </summary>
public sealed class HanshowHeartbeatEndpoint(IHanshowHeartbeatProcessor processor)
    : Endpoint<HanshowHeartbeatRequest, EmptyResponse>
{
    private readonly IHanshowHeartbeatProcessor processor = processor;

    /// <summary>
    /// Configures the endpoint route, version, and access rules.
    /// </summary>
    public override void Configure()
    {
        Post("/Hanshow/Heartbeat");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("internal")));
    }

    /// <summary>
    /// Handles the incoming request and writes the HTTP response.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    public override async Task HandleAsync(HanshowHeartbeatRequest request, CancellationToken ct)
    {
        HanshowHeartbeatData data = new(request.ShortSerial, request.LongSerial);
        await this.processor.ProcessAsync(data, ct).ConfigureAwait(false);
        this.HttpContext.Response.StatusCode = StatusCodes.Status204NoContent;
    }
}
