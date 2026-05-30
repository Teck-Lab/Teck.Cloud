// <copyright file="CustomerLicenseValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Domain.Entities.LicenseAggregate;
using Customer.Domain.Entities.LicenseAggregate.Repositories;
using SharedKernel.Core.Licensing;

namespace Customer.Infrastructure.Licensing;

/// <summary>
/// Validates licenses by querying the Customer service database.
/// </summary>
public sealed class CustomerLicenseValidator : ILicenseValidator
{
    private readonly ILicenseWriteRepository licenseRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerLicenseValidator"/> class.
    /// </summary>
    /// <param name="licenseRepository">The license repository.</param>
    public CustomerLicenseValidator(ILicenseWriteRepository licenseRepository)
    {
        this.licenseRepository = licenseRepository ?? throw new ArgumentNullException(nameof(licenseRepository));
    }

    /// <inheritdoc/>
    public async Task<LicenseValidationResult> ValidateAsync(
        string tenantId,
        string? locationId,
        CancellationToken cancellationToken)
    {
        License? license = locationId is not null
            ? await this.licenseRepository.GetActiveByLocationIdAsync(locationId, cancellationToken).ConfigureAwait(false)
            : await this.licenseRepository.GetActiveByTenantIdAsync(tenantId, cancellationToken).ConfigureAwait(false);

        if (license is null)
        {
            return LicenseValidationResult.Failure("No active license found.");
        }

        if (!license.Status.IsUsable)
        {
            return LicenseValidationResult.Failure($"License is {license.Status.Name}.");
        }

        return LicenseValidationResult.Success(
            null, // We don't enforce quotas at the validator level — services check individually
            null,
            null,
            null,
            false,
            false);
    }
}
