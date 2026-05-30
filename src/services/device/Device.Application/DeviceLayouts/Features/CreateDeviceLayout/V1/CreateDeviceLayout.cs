// <copyright file="CreateDeviceLayout.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceDefinitions.Abstractions;
using Device.Application.DeviceLayouts.Abstractions;
using Device.Domain.Entities.DeviceLayoutAggregate;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;

namespace Device.Application.DeviceLayouts.Features.CreateDeviceLayout.V1;

/// <summary>
/// Command to create a new device layout for a given device definition.
/// </summary>
/// <param name="DeviceDefinitionId">The device definition this layout belongs to.</param>
/// <param name="Name">Human-readable layout name.</param>
/// <param name="MaxZoneCount">Maximum number of content zones.</param>
public sealed record CreateDeviceLayoutCommand(
    Guid DeviceDefinitionId,
    string Name,
    int MaxZoneCount)
    : ICommand<ErrorOr<CreateDeviceLayoutResponse>>;

/// <summary>
/// Handler for <see cref="CreateDeviceLayoutCommand"/>.
/// </summary>
internal sealed class CreateDeviceLayoutCommandHandler(
    IDeviceDefinitionReadRepository deviceDefinitionReadRepository,
    IDeviceLayoutWriteRepository writeRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateDeviceLayoutCommand, ErrorOr<CreateDeviceLayoutResponse>>
{
    private readonly IDeviceDefinitionReadRepository deviceDefinitionReadRepository = deviceDefinitionReadRepository;
    private readonly IDeviceLayoutWriteRepository writeRepository = writeRepository;
    private readonly IUnitOfWork unitOfWork = unitOfWork;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<CreateDeviceLayoutResponse>> Handle(
        CreateDeviceLayoutCommand request,
        CancellationToken cancellationToken)
    {
        DeviceDefinitionSnapshot? definition = await this.deviceDefinitionReadRepository
            .GetByIdAsync(request.DeviceDefinitionId, cancellationToken)
            .ConfigureAwait(false);

        if (definition is null)
        {
            return Error.NotFound(
                "DeviceDefinition.NotFound",
                $"No device definition with ID '{request.DeviceDefinitionId}' was found.");
        }

        ErrorOr<DeviceLayout> created = DeviceLayout.Create(
            request.DeviceDefinitionId,
            request.Name,
            request.MaxZoneCount);

        if (created.IsError)
        {
            return created.Errors;
        }

        await this.writeRepository.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        await this.unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CreateDeviceLayoutResponse(created.Value.Id);
    }
}
