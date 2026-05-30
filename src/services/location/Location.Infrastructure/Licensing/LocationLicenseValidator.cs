// <copyright file="LocationLicenseValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Licensing;

namespace Location.Infrastructure.Licensing;

/// <summary>
/// Validates licenses for the Location service.
/// </summary>
/// <remarks>
/// This is a stub implementation that always returns success.
/// Real validation requires cross-service integration with the Customer service
/// to query active licenses, which is not yet implemented.
/// Future work: Replace with a real implementation that calls Customer service or a shared cache.
/// </remarks>
public sealed class LocationLicenseValidator : ILicenseValidator
{
    /// <inheritdoc/>
    public Task<LicenseValidationResult> ValidateAsync(
        string tenantId,
        string? locationId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(LicenseValidationResult.Success(
            maxAccessPoints: null,
            maxDevices: null,
            maxProducts: null,
            maxLocations: null,
            supportsCustomBranding: false,
            supportsAnalytics: false));
    }
}
