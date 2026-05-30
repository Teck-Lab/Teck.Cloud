// <copyright file="AssignLicenseLocationCommandHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Licenses.Responses;
using Customer.Domain.Entities.LicenseAggregate;
using Customer.Domain.Entities.LicenseAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Licenses.Features.AssignLicenseLocation.V1;

/// <summary>
/// Handler for <see cref="AssignLicenseLocationCommand"/>.
/// </summary>
public sealed class AssignLicenseLocationCommandHandler : ICommandHandler<AssignLicenseLocationCommand, ErrorOr<LicenseResponse>>
{
    private readonly ILicenseWriteRepository licenseRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssignLicenseLocationCommandHandler"/> class.
    /// </summary>
    /// <param name="licenseRepository">The license repository.</param>
    public AssignLicenseLocationCommandHandler(ILicenseWriteRepository licenseRepository)
    {
        this.licenseRepository = licenseRepository ?? throw new ArgumentNullException(nameof(licenseRepository));
    }

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<LicenseResponse>> Handle(AssignLicenseLocationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        License? license = await this.licenseRepository.GetByIdAsync(command.LicenseId, cancellationToken).ConfigureAwait(false);

        if (license is null)
        {
            return Error.NotFound("License.NotFound", $"License with ID '{command.LicenseId}' not found.");
        }

        license.SetLocationId(command.LocationId);
        await this.licenseRepository.UpdateAsync(license, cancellationToken).ConfigureAwait(false);

        return MapToResponse(license);
    }

    private static LicenseResponse MapToResponse(License license)
    {
        return new LicenseResponse
        {
            Id = license.Id,
            TenantId = license.TenantId,
            LocationId = license.LocationId,
            Plan = license.Plan,
            Status = license.Status.Name,
            ExpiresAt = license.ExpiresAt,
            GracePeriodEndsAt = license.GracePeriodEndsAt,
            PaymentMethodId = license.PaymentMethodId,
            PaymentScope = license.PaymentScope,
            CreatedAt = license.CreatedAt,
            UpdatedAt = license.UpdatedAt,
        };
    }
}
