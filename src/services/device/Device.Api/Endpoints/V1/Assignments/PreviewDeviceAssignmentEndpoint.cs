// <copyright file="PreviewDeviceAssignmentEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Device.Application.Assignments.Features.PreviewDeviceAssignment.V1;
using ErrorOr;
using FastEndpoints;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Device.Api.Endpoints.V1.Assignments;

public sealed class PreviewDeviceAssignmentEndpoint(ISender sender)
    : Endpoint<PreviewDeviceAssignmentRequest, PreviewDeviceAssignmentResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Post("/Assignments/Preview");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("internal")));
    }

    public override async Task HandleAsync(PreviewDeviceAssignmentRequest request, CancellationToken ct)
    {
        PreviewDeviceAssignmentQuery query = new(
            request.DeviceId,
            request.LocationNodeId,
            request.TemplateId,
            request.Zones);

        ErrorOr<PreviewDeviceAssignmentResponse> queryResponse = await this.sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(queryResponse, cancellation: ct).ConfigureAwait(false);
    }
}
