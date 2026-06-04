// <copyright file="RegisterAccessPointEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062
using Device.Application.AccessPoints.Features.RegisterAccessPoint.V1;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Device.Api.Endpoints.V1.AccessPoints;

/// <summary>
/// Handles register access point requests.
/// </summary>
public sealed class RegisterAccessPointEndpoint(ISender sender)
    : Endpoint<RegisterAccessPointRequest, RegisterAccessPointResponse>
{
    private readonly ISender sender = sender;

    /// <summary>
    /// Configures the endpoint route, version, and access rules.
    /// </summary>
    public override void Configure()
    {
        Post("/AccessPoints/Register");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("access-point", "create");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
    }

    /// <summary>
    /// Handles the incoming request and writes the HTTP response.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    public override async Task HandleAsync(RegisterAccessPointRequest request, CancellationToken ct)
    {
        RegisterAccessPointCommand command = new(
            request.SerialNumber,
            request.Vendor,
            request.LocationNodeId,
            request.MaxCapacity);

        ErrorOr<RegisterAccessPointResponse> result = await this.sender.Send(command, ct).ConfigureAwait(false);
        await this.SendAsync(result, cancellation: ct).ConfigureAwait(false);
    }
}
