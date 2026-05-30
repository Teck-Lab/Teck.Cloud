// <copyright file="RegisterAccessPoint.cs" company="TeckLab">
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

namespace Device.Application.AccessPoints.Features.RegisterAccessPoint.V1;

/// <summary>
/// Command to register an access point at a location node.
/// </summary>
/// <param name="SerialNumber">Supplier serial number.</param>
/// <param name="Vendor">Vendor name.</param>
/// <param name="LocationNodeId">Location node identifier.</param>
/// <param name="MaxCapacity">Maximum supported display count.</param>
public sealed record RegisterAccessPointCommand(
    string SerialNumber,
    string Vendor,
    string LocationNodeId,
    int MaxCapacity)
    : ICommand<ErrorOr<RegisterAccessPointResponse>>;

/// <summary>
/// Response from registering an access point.
/// </summary>
/// <param name="AccessPointId">Created access point identifier.</param>
public sealed record RegisterAccessPointResponse(Guid AccessPointId);

/// <summary>
/// Handler for <see cref="RegisterAccessPointCommand"/>.
/// </summary>
internal sealed class RegisterAccessPointCommandHandler(
    DomainAccessPointReadRepository readRepository,
    DomainAccessPointWriteRepository writeRepository,
    IUnitOfWork unitOfWork,
    IMessageBus messageBus)
    : ICommandHandler<RegisterAccessPointCommand, ErrorOr<RegisterAccessPointResponse>>
{
    private readonly DomainAccessPointReadRepository readRepository = readRepository;
    private readonly DomainAccessPointWriteRepository writeRepository = writeRepository;
    private readonly IUnitOfWork unitOfWork = unitOfWork;
    private readonly IMessageBus messageBus = messageBus;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<RegisterAccessPointResponse>> Handle(
        RegisterAccessPointCommand request,
        CancellationToken cancellationToken)
    {
        string normalisedSerial = request.SerialNumber.Trim().ToUpperInvariant();

        AccessPoint? existing = await this.readRepository
            .GetBySerialAsync(normalisedSerial, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return Error.Conflict(
                "AccessPoint.DuplicateSerialNumber",
                $"An access point with serial number '{normalisedSerial}' already exists.");
        }

        ErrorOr<AccessPoint> created = AccessPoint.Create(
            normalisedSerial,
            request.Vendor,
            request.LocationNodeId,
            request.MaxCapacity);

        if (created.IsError)
        {
            return created.Errors;
        }

        AccessPoint accessPoint = created.Value;

        await this.writeRepository.AddAsync(accessPoint, cancellationToken).ConfigureAwait(false);
        await this.unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await this.messageBus
            .PublishAsync(new AccessPointRegisteredIntegrationEvent(
                accessPoint.Id,
                accessPoint.SerialNumber,
                accessPoint.Vendor,
                accessPoint.LocationNodeId,
                accessPoint.MaxCapacity,
                DateTimeOffset.UtcNow))
            .ConfigureAwait(false);

        return new RegisterAccessPointResponse(accessPoint.Id);
    }
}
