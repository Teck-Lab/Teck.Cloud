// <copyright file="GetLicenseByIdQueryHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Licenses.Responses;
using Customer.Domain.Entities.LicenseAggregate;
using Customer.Domain.Entities.LicenseAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Licenses.Features.GetLicenseById.V1;

/// <summary>
/// Handler for <see cref="GetLicenseByIdQuery"/>.
/// </summary>
public sealed class GetLicenseByIdQueryHandler : IQueryHandler<GetLicenseByIdQuery, ErrorOr<LicenseResponse>>
{
    private readonly ILicenseWriteRepository licenseRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetLicenseByIdQueryHandler"/> class.
    /// </summary>
    /// <param name="licenseRepository">The license repository.</param>
    public GetLicenseByIdQueryHandler(ILicenseWriteRepository licenseRepository)
    {
        this.licenseRepository = licenseRepository ?? throw new ArgumentNullException(nameof(licenseRepository));
    }

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<LicenseResponse>> Handle(GetLicenseByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        License? license = await this.licenseRepository.GetByIdAsync(query.LicenseId, cancellationToken).ConfigureAwait(false);

        if (license is null)
        {
            return Error.NotFound("License.NotFound", $"License with ID '{query.LicenseId}' not found.");
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
