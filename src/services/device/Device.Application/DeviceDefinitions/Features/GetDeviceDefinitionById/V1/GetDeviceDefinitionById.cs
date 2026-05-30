// <copyright file="GetDeviceDefinitionById.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceDefinitions.Abstractions;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Device.Application.DeviceDefinitions.Features.GetDeviceDefinitionById.V1;

/// <summary>
/// Query for a single device definition by its identifier.
/// </summary>
/// <param name="Id">The device definition identifier.</param>
public sealed record GetDeviceDefinitionByIdQuery(Guid Id)
    : IQuery<ErrorOr<GetDeviceDefinitionByIdResponse>>;

/// <summary>
/// Handler for <see cref="GetDeviceDefinitionByIdQuery"/>.
/// </summary>
internal sealed class GetDeviceDefinitionByIdQueryHandler(IDeviceDefinitionReadRepository repository)
    : IQueryHandler<GetDeviceDefinitionByIdQuery, ErrorOr<GetDeviceDefinitionByIdResponse>>
{
    private readonly IDeviceDefinitionReadRepository repository = repository;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<GetDeviceDefinitionByIdResponse>> Handle(
        GetDeviceDefinitionByIdQuery request,
        CancellationToken cancellationToken)
    {
        DeviceDefinitionSnapshot? snapshot = await this.repository
            .GetByIdAsync(request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (snapshot is null)
        {
            return Error.NotFound(
                "DeviceDefinition.NotFound",
                $"No device definition with ID '{request.Id}' was found.");
        }

        return new GetDeviceDefinitionByIdResponse(
            snapshot.Id,
            snapshot.ModelId,
            snapshot.Name,
            snapshot.WidthPx,
            snapshot.HeightPx,
            (int)snapshot.SupportedColors,
            snapshot.SupportsNfc,
            snapshot.EslProvider.Name,
            snapshot.CatalogManufacturerId,
            snapshot.CatalogSupplierId,
            snapshot.CatalogProductId);
    }
}
