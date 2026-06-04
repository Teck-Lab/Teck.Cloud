// <copyright file="RegisterDeviceDefinitionEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062
using Device.Application.DeviceDefinitions.Features.RegisterDeviceDefinition.V1;
using ErrorOr;
using FastEndpoints;
using Mediator;
using SharedKernel.Core.Devices;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Device.Api.Endpoints.V1.DeviceDefinitions;

/// <summary>
/// Handles register device definition requests.
/// </summary>
public sealed class RegisterDeviceDefinitionEndpoint(ISender sender)
    : Endpoint<RegisterDeviceDefinitionRequest, RegisterDeviceDefinitionResponse>
{
    private readonly ISender sender = sender;

    /// <summary>
    /// Configures the endpoint route, version, and access rules.
    /// </summary>
    public override void Configure()
    {
        Post("/DeviceDefinitions");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    /// <summary>
    /// Handles the incoming request and writes the HTTP response.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    public override async Task HandleAsync(RegisterDeviceDefinitionRequest request, CancellationToken ct)
    {
        if (!EslProvider.TryFromName(request.EslProvider, false, out EslProvider? eslProvider))
        {
            ErrorOr<RegisterDeviceDefinitionResponse> validationError = Error.Validation(
                "DeviceDefinition.InvalidEslProvider",
                $"'{request.EslProvider}' is not a valid ESL provider.");
            await this.SendAsync(validationError, cancellation: ct).ConfigureAwait(false);
            return;
        }

        RegisterDeviceDefinitionCommand command = new(
            request.ModelId,
            request.Name,
            eslProvider,
            (Domain.Entities.DeviceDefinitionAggregate.DisplayInkColor)request.SupportedColors,
            request.SupportsNfc,
            request.WidthPx,
            request.HeightPx,
            request.CatalogManufacturerId,
            request.CatalogSupplierId,
            request.CatalogProductId);

        ErrorOr<RegisterDeviceDefinitionResponse> result = await this.sender.Send(command, ct).ConfigureAwait(false);
        await this.SendAsync(result, cancellation: ct).ConfigureAwait(false);
    }
}
