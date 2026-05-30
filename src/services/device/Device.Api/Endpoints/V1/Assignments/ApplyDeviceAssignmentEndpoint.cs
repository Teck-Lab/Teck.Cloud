// <copyright file="ApplyDeviceAssignmentEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Device.Application.Assignments.Features.ApplyDeviceAssignment.V1;
using ErrorOr;
using FastEndpoints;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Device.Api.Endpoints.V1.Assignments;

public sealed class ApplyDeviceAssignmentEndpoint(ISender sender)
    : Endpoint<ApplyDeviceAssignmentRequest, ApplyDeviceAssignmentResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Post("/Assignments/Apply");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("internal")));
    }

    public override async Task HandleAsync(ApplyDeviceAssignmentRequest request, CancellationToken ct)
    {
        ApplyDeviceAssignmentCommand command = new(
            request.DeviceId,
            request.LocationNodeId,
            request.TemplateId,
            request.Zones);

        ErrorOr<ApplyDeviceAssignmentResponse> commandResponse = await this.sender.Send(command, ct).ConfigureAwait(false);
        await this.SendAsync(commandResponse, cancellation: ct).ConfigureAwait(false);
    }
}
