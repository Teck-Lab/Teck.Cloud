// <copyright file="CreateLicenseCommandHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Common.Interfaces;
using Customer.Application.Licenses.Responses;
using Customer.Domain.Entities.LicenseAggregate;
using Customer.Domain.Entities.LicenseAggregate.Repositories;
using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Pricing;

namespace Customer.Application.Licenses.Features.CreateLicense.V1;

/// <summary>
/// Handler for <see cref="CreateLicenseCommand"/>.
/// </summary>
public sealed class CreateLicenseCommandHandler : ICommandHandler<CreateLicenseCommand, ErrorOr<LicenseResponse>>
{
    private readonly ILicenseWriteRepository licenseRepository;
    private readonly ITenantWriteRepository tenantRepository;
    private readonly ILicenseIssuer licenseIssuer;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateLicenseCommandHandler"/> class.
    /// </summary>
    /// <param name="licenseRepository">The license repository.</param>
    /// <param name="tenantRepository">The tenant repository.</param>
    /// <param name="licenseIssuer">The license issuer service.</param>
    public CreateLicenseCommandHandler(
        ILicenseWriteRepository licenseRepository,
        ITenantWriteRepository tenantRepository,
        ILicenseIssuer licenseIssuer)
    {
        this.licenseRepository = licenseRepository ?? throw new ArgumentNullException(nameof(licenseRepository));
        this.tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
        this.licenseIssuer = licenseIssuer ?? throw new ArgumentNullException(nameof(licenseIssuer));
    }

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<LicenseResponse>> Handle(CreateLicenseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        TenantPlan tenantPlan = TenantPlan.FromName(command.Plan, false);

        string licenseXml = await this.licenseIssuer.IssueLicenseAsync(
            command.TenantId,
            command.LocationId,
            command.Plan,
            tenantPlan,
            command.PaymentMethodId,
            command.PaymentScope,
            cancellationToken).ConfigureAwait(false);

        DateTimeOffset expiresAt = tenantPlan.IsTrial
            ? DateTimeOffset.UtcNow.Add(TenantPlan.TrialDuration)
            : DateTimeOffset.UtcNow.AddYears(1);

        var licenseArgs = new LicenseCreateArgs
        {
            TenantId = command.TenantId,
            LocationId = command.LocationId,
            Plan = command.Plan,
            LicenseXml = licenseXml,
            ExpiresAt = expiresAt,
            PaymentMethodId = command.PaymentMethodId,
            PaymentScope = command.PaymentScope,
        };

        ErrorOr<License> licenseResult = License.Create(licenseArgs);
        if (licenseResult.IsError)
        {
            return licenseResult.Errors;
        }

        License license = licenseResult.Value;

        if (!tenantPlan.IsTrial)
        {
            license.Activate();
        }

        await this.licenseRepository.AddAsync(license, cancellationToken).ConfigureAwait(false);

        if (command.LocationId is null)
        {
            Tenant? tenant = await this.tenantRepository.GetByIdAsync(
                Guid.Parse(command.TenantId), cancellationToken).ConfigureAwait(false);

            if (tenant is not null)
            {
                tenant.AssignLicense(license.Id);
                this.tenantRepository.Update(tenant);
            }
        }

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
