// <copyright file="UpdateAccessPointStatusEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Device.Application.AccessPoints.Features.UpdateAccessPointStatus.V1;
using Device.Domain.AccessPoints;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Device.Api.Endpoints.V1.AccessPoints;

public sealed class UpdateAccessPointStatusEndpoint(ISender sender)
    : Endpoint<UpdateAccessPointStatusRequest, UpdateAccessPointStatusResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Post("/AccessPoints/{Serial}/Status");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("access-point", "update");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
    }

    public override async Task HandleAsync(UpdateAccessPointStatusRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse(request.Status, ignoreCase: true, out AccessPointStatus status))
        {
            ErrorOr<UpdateAccessPointStatusResponse> validationError = Error.Validation(
                "AccessPoint.InvalidStatus",
                $"'{request.Status}' is not a valid access point status.");
            await this.SendAsync(validationError, cancellation: ct).ConfigureAwait(false);
            return;
        }

        UpdateAccessPointStatusCommand command = new(request.Serial, status);
        ErrorOr<UpdateAccessPointStatusResponse> result = await this.sender.Send(command, ct).ConfigureAwait(false);
        await this.SendAsync(result, cancellation: ct).ConfigureAwait(false);
    }
}
