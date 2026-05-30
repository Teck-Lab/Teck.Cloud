// <copyright file="UpdateAccessPointStatus.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.AccessPoints;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;
using SharedKernel.Events;
using Wolverine;
using DomainAccessPointReadRepository = Device.Domain.AccessPoints.IAccessPointReadRepository;
using DomainAccessPointWriteRepository = Device.Domain.AccessPoints.IAccessPointWriteRepository;

namespace Device.Application.AccessPoints.Features.UpdateAccessPointStatus.V1;

/// <summary>
/// Command to update an access point status.
/// </summary>
/// <param name="SerialNumber">Supplier serial number.</param>
/// <param name="NewStatus">New operational status.</param>
public sealed record UpdateAccessPointStatusCommand(string SerialNumber, AccessPointStatus NewStatus)
    : ICommand<ErrorOr<UpdateAccessPointStatusResponse>>;

/// <summary>
/// Response from updating access point status.
/// </summary>
/// <param name="AccessPointId">Updated access point identifier.</param>
/// <param name="Status">Current operational status.</param>
public sealed record UpdateAccessPointStatusResponse(Guid AccessPointId, AccessPointStatus Status);

/// <summary>
/// Handler for <see cref="UpdateAccessPointStatusCommand"/>.
/// </summary>
internal sealed class UpdateAccessPointStatusCommandHandler(
    DomainAccessPointReadRepository readRepository,
    DomainAccessPointWriteRepository writeRepository,
    IUnitOfWork unitOfWork,
    IMessageBus messageBus)
    : ICommandHandler<UpdateAccessPointStatusCommand, ErrorOr<UpdateAccessPointStatusResponse>>
{
    private readonly DomainAccessPointReadRepository readRepository = readRepository;
    private readonly DomainAccessPointWriteRepository writeRepository = writeRepository;
    private readonly IUnitOfWork unitOfWork = unitOfWork;
    private readonly IMessageBus messageBus = messageBus;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<UpdateAccessPointStatusResponse>> Handle(
        UpdateAccessPointStatusCommand request,
        CancellationToken cancellationToken)
    {
        string normalisedSerial = request.SerialNumber.Trim().ToUpperInvariant();

        AccessPoint? accessPoint = await this.readRepository
            .GetBySerialAsync(normalisedSerial, cancellationToken)
            .ConfigureAwait(false);

        if (accessPoint is null)
        {
            return Error.NotFound(
                "AccessPoint.NotFound",
                $"Access point '{normalisedSerial}' was not found.");
        }

        AccessPointStatus previousStatus = accessPoint.Status;
        accessPoint.SetStatus(request.NewStatus);

        await this.writeRepository.UpdateAsync(accessPoint, cancellationToken).ConfigureAwait(false);
        await this.unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await this.messageBus
            .PublishAsync(new AccessPointStatusChangedIntegrationEvent(
                accessPoint.Id,
                accessPoint.SerialNumber,
                previousStatus.ToString(),
                accessPoint.Status.ToString(),
                accessPoint.LocationNodeId,
                DateTimeOffset.UtcNow))
            .ConfigureAwait(false);

        return new UpdateAccessPointStatusResponse(accessPoint.Id, accessPoint.Status);
    }
}
