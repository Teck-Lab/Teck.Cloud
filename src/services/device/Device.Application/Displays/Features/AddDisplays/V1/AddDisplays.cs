// <copyright file="AddDisplays.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DisplayAggregate;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;

namespace Device.Application.Displays.Features.AddDisplays.V1;

/// <summary>
/// Command to batch-register displays to a location node.
/// </summary>
/// <param name="LocationNodeId">Location node the displays belong to.</param>
/// <param name="Serials">Short serials to register.</param>
/// <param name="DeviceDefinitionId">Optional device model identifier.</param>
public sealed record AddDisplaysCommand(
    string LocationNodeId,
    IReadOnlyList<string> Serials,
    Guid? DeviceDefinitionId)
    : ICommand<ErrorOr<AddDisplaysResponse>>;

/// <summary>
/// Handler for <see cref="AddDisplaysCommand"/>.
/// </summary>
internal sealed class AddDisplaysCommandHandler(
    Abstractions.IDisplayWriteRepository displayWriteRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddDisplaysCommand, ErrorOr<AddDisplaysResponse>>
{
    private readonly Abstractions.IDisplayWriteRepository displayWriteRepository = displayWriteRepository;
    private readonly IUnitOfWork unitOfWork = unitOfWork;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<AddDisplaysResponse>> Handle(
        AddDisplaysCommand request,
        CancellationToken cancellationToken)
    {
        List<AddDisplayResult> results = new(request.Serials.Count);
        int addedCount = 0;
        int duplicateCount = 0;

        foreach (string serial in request.Serials)
        {
            string normalised = serial.Trim().ToUpperInvariant();

            bool isDuplicate = await this.displayWriteRepository
                .ExistsWithShortSerialGlobalAsync(normalised, cancellationToken)
                .ConfigureAwait(false);

            if (isDuplicate)
            {
                results.Add(new AddDisplayResult(normalised, DisplayId: null, Duplicate: true));
                duplicateCount++;
                continue;
            }

            ErrorOr<Display> created = Display.Create(normalised, request.LocationNodeId, request.DeviceDefinitionId);
            if (created.IsError)
            {
                return created.Errors;
            }

            await this.displayWriteRepository.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
            results.Add(new AddDisplayResult(normalised, created.Value.Id, Duplicate: false));
            addedCount++;
        }

        if (addedCount > 0)
        {
            await this.unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new AddDisplaysResponse(results, addedCount, duplicateCount);
    }
}
