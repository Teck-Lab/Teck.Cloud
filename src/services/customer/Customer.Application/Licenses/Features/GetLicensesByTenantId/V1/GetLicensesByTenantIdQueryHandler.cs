// <copyright file="GetLicensesByTenantIdQueryHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Licenses.Responses;
using Customer.Domain.Entities.LicenseAggregate;
using Customer.Domain.Entities.LicenseAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Licenses.Features.GetLicensesByTenantId.V1;

/// <summary>
/// Handler for <see cref="GetLicensesByTenantIdQuery"/>.
/// </summary>
public sealed class GetLicensesByTenantIdQueryHandler : IQueryHandler<GetLicensesByTenantIdQuery, ErrorOr<IReadOnlyList<LicenseResponse>>>
{
    private readonly ILicenseWriteRepository licenseRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetLicensesByTenantIdQueryHandler"/> class.
    /// </summary>
    /// <param name="licenseRepository">The license repository.</param>
    public GetLicensesByTenantIdQueryHandler(ILicenseWriteRepository licenseRepository)
    {
        this.licenseRepository = licenseRepository ?? throw new ArgumentNullException(nameof(licenseRepository));
    }

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<IReadOnlyList<LicenseResponse>>> Handle(GetLicensesByTenantIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IReadOnlyList<License> licenses = await this.licenseRepository.GetByTenantIdAsync(query.TenantId, cancellationToken).ConfigureAwait(false);

        return licenses.Select(MapToResponse).ToList();
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
