// <copyright file="RevokeLicenseCommandHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Licenses.Responses;
using Customer.Domain.Entities.LicenseAggregate;
using Customer.Domain.Entities.LicenseAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Licenses.Features.RevokeLicense.V1;

/// <summary>
/// Handler for <see cref="RevokeLicenseCommand"/>.
/// </summary>
public sealed class RevokeLicenseCommandHandler : ICommandHandler<RevokeLicenseCommand, ErrorOr<LicenseResponse>>
{
    private readonly ILicenseWriteRepository licenseRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="RevokeLicenseCommandHandler"/> class.
    /// </summary>
    /// <param name="licenseRepository">The license repository.</param>
    public RevokeLicenseCommandHandler(ILicenseWriteRepository licenseRepository)
    {
        this.licenseRepository = licenseRepository ?? throw new ArgumentNullException(nameof(licenseRepository));
    }

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<LicenseResponse>> Handle(RevokeLicenseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        License? license = await this.licenseRepository.GetByIdAsync(command.LicenseId, cancellationToken).ConfigureAwait(false);

        if (license is null)
        {
            return Error.NotFound("License.NotFound", $"License with ID '{command.LicenseId}' not found.");
        }

        license.Revoke();
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
