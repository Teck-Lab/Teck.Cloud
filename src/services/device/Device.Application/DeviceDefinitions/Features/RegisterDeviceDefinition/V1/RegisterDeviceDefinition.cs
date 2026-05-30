// <copyright file="RegisterDeviceDefinition.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceDefinitions.Abstractions;
using Device.Domain.Entities.DeviceDefinitionAggregate;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;
using SharedKernel.Core.Devices;

namespace Device.Application.DeviceDefinitions.Features.RegisterDeviceDefinition.V1;

/// <summary>
/// Command to register a new device definition hardware model.
/// </summary>
/// <param name="ModelId">Unique supplier model code.</param>
/// <param name="Name">Human-readable model name.</param>
/// <param name="EslProvider">ESL vendor integration driver.</param>
/// <param name="SupportedColors">Supported ink colour bitmask.</param>
/// <param name="SupportsNfc">Whether the model supports NFC.</param>
/// <param name="WidthPx">Optional screen width in pixels.</param>
/// <param name="HeightPx">Optional screen height in pixels.</param>
/// <param name="CatalogManufacturerId">Optional Catalog manufacturer soft-link.</param>
/// <param name="CatalogSupplierId">Optional Catalog supplier soft-link.</param>
/// <param name="CatalogProductId">Optional Catalog product soft-link.</param>
public sealed record RegisterDeviceDefinitionCommand(
    string ModelId,
    string Name,
    EslProvider EslProvider,
    DisplayInkColor SupportedColors,
    bool SupportsNfc,
    int? WidthPx,
    int? HeightPx,
    Guid? CatalogManufacturerId,
    Guid? CatalogSupplierId,
    Guid? CatalogProductId)
    : ICommand<ErrorOr<RegisterDeviceDefinitionResponse>>;

/// <summary>
/// Handler for <see cref="RegisterDeviceDefinitionCommand"/>.
/// </summary>
internal sealed class RegisterDeviceDefinitionCommandHandler(
    IDeviceDefinitionWriteRepository writeRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RegisterDeviceDefinitionCommand, ErrorOr<RegisterDeviceDefinitionResponse>>
{
    private readonly IDeviceDefinitionWriteRepository writeRepository = writeRepository;
    private readonly IUnitOfWork unitOfWork = unitOfWork;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<RegisterDeviceDefinitionResponse>> Handle(
        RegisterDeviceDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        bool exists = await this.writeRepository
            .ExistsWithModelIdAsync(request.ModelId, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            return Error.Conflict(
                "DeviceDefinition.DuplicateModelId",
                $"A device definition with model ID '{request.ModelId}' already exists.");
        }

        ErrorOr<DeviceDefinition> created = DeviceDefinition.Create(
            request.ModelId,
            request.Name,
            request.EslProvider,
            request.SupportedColors,
            request.SupportsNfc,
            request.WidthPx,
            request.HeightPx,
            request.CatalogManufacturerId,
            request.CatalogSupplierId,
            request.CatalogProductId);

        if (created.IsError)
        {
            return created.Errors;
        }

        await this.writeRepository.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        await this.unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new RegisterDeviceDefinitionResponse(created.Value.Id);
    }
}
