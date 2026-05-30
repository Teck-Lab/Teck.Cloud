// <copyright file="RenewLicenseCommandHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Common.Interfaces;
using Customer.Application.Licenses.Responses;
using Customer.Domain.Entities.LicenseAggregate;
using Customer.Domain.Entities.LicenseAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Pricing;

namespace Customer.Application.Licenses.Features.RenewLicense.V1;

/// <summary>
/// Handler for <see cref="RenewLicenseCommand"/>.
/// </summary>
public sealed class RenewLicenseCommandHandler : ICommandHandler<RenewLicenseCommand, ErrorOr<LicenseResponse>>
{
    private readonly ILicenseWriteRepository licenseRepository;
    private readonly ILicenseIssuer licenseIssuer;

    /// <summary>
    /// Initializes a new instance of the <see cref="RenewLicenseCommandHandler"/> class.
    /// </summary>
    /// <param name="licenseRepository">The license repository.</param>
    /// <param name="licenseIssuer">The license issuer service.</param>
    public RenewLicenseCommandHandler(
        ILicenseWriteRepository licenseRepository,
        ILicenseIssuer licenseIssuer)
    {
        this.licenseRepository = licenseRepository ?? throw new ArgumentNullException(nameof(licenseRepository));
        this.licenseIssuer = licenseIssuer ?? throw new ArgumentNullException(nameof(licenseIssuer));
    }

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<LicenseResponse>> Handle(RenewLicenseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        License? license = await this.licenseRepository.GetByIdAsync(command.LicenseId, cancellationToken).ConfigureAwait(false);

        if (license is null)
        {
            return Error.NotFound("License.NotFound", $"License with ID '{command.LicenseId}' not found.");
        }

        TenantPlan tenantPlan = TenantPlan.FromName(command.NewPlan, false);

        string newLicenseXml = await this.licenseIssuer.IssueLicenseAsync(
            license.TenantId,
            license.LocationId,
            command.NewPlan,
            tenantPlan,
            license.PaymentMethodId,
            license.PaymentScope,
            cancellationToken).ConfigureAwait(false);

        license.Renew(newLicenseXml, command.NewExpiry);
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
