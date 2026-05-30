// <copyright file="LicenseIssuer.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Common.Interfaces;
using Customer.Infrastructure.Licensing.Abstractions;
using SharedKernel.Core.Pricing;
using Standard.Licensing;

namespace Customer.Infrastructure.Licensing;

/// <summary>
/// Issues signed software licenses using Standard.Licensing.
/// </summary>
public sealed class LicenseIssuer : ILicenseIssuer
{
    private readonly ILicenseKeyProvider keyProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="LicenseIssuer"/> class.
    /// </summary>
    /// <param name="keyProvider">The key provider for signing.</param>
    public LicenseIssuer(ILicenseKeyProvider keyProvider)
    {
        this.keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
    }

    /// <summary>
    /// Issues a new signed license for a tenant or location.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="locationId">The location identifier, or null for tenant-level licensing.</param>
    /// <param name="plan">The plan name.</param>
    /// <param name="tenantPlan">The tenant plan with quota metadata.</param>
    /// <param name="paymentMethodId">The payment method identifier.</param>
    /// <param name="paymentScope">The payment scope — "TenantDefault" or "LocationOverride".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The signed license XML string.</returns>
    public async Task<string> IssueLicenseAsync(
        string tenantId,
        string? locationId,
        string plan,
        TenantPlan tenantPlan,
        string? paymentMethodId,
        string paymentScope,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentScope);
        ArgumentNullException.ThrowIfNull(tenantPlan);

        string privateKey = await this.keyProvider.GetPrivateKeyAsync(cancellationToken).ConfigureAwait(false);
        string passphrase = await this.keyProvider.GetPassphraseAsync(cancellationToken).ConfigureAwait(false);

        DateTimeOffset expiry = tenantPlan.IsTrial
            ? DateTimeOffset.UtcNow.Add(TenantPlan.TrialDuration)
            : DateTimeOffset.UtcNow.AddYears(1);

        var license = Standard.Licensing.License.New()
            .WithUniqueIdentifier(Guid.NewGuid())
            .As(LicenseType.Standard)
            .ExpiresAt(expiry.UtcDateTime)
            .LicensedTo(tenantId, string.Empty)
            .WithProductFeatures(new Dictionary<string, string>
            {
                [LicenseFeatureKey.AccessPointsMax] = tenantPlan.MaxAccessPointsPerLocation?.ToString() ?? "unlimited",
                [LicenseFeatureKey.DevicesMax] = tenantPlan.MaxDevicesPerLocation?.ToString() ?? "unlimited",
                [LicenseFeatureKey.ProductsMax] = tenantPlan.MaxProductsPerLocation?.ToString() ?? "unlimited",
                [LicenseFeatureKey.MaxLocations] = tenantPlan.MaxLocations.ToString(),
                [LicenseFeatureKey.SupportsCustomBranding] = tenantPlan.SupportsCustomBranding.ToString(),
                [LicenseFeatureKey.SupportsAnalytics] = tenantPlan.SupportsAnalytics.ToString(),
            })
            .WithAdditionalAttributes(new Dictionary<string, string>
            {
                [LicenseFeatureKey.TenantId] = tenantId,
                [LicenseFeatureKey.LocationId] = locationId ?? "tenant-level",
                [LicenseFeatureKey.Plan] = plan,
                [LicenseFeatureKey.PaymentScope] = paymentScope,
                [LicenseFeatureKey.PaymentMethodId] = paymentMethodId ?? string.Empty,
            })
            .CreateAndSignWithPrivateKey(privateKey, passphrase);

        return license.ToString();
    }

    /// <inheritdoc/>
    public async Task<string> IssueLicenseWithOverridesAsync(
        string tenantId,
        string? locationId,
        string plan,
        TenantPlan tenantPlan,
        IReadOnlyDictionary<string, string> featureOverrides,
        string? paymentMethodId,
        string paymentScope,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentScope);
        ArgumentNullException.ThrowIfNull(tenantPlan);
        ArgumentNullException.ThrowIfNull(featureOverrides);

        string privateKey = await this.keyProvider.GetPrivateKeyAsync(cancellationToken).ConfigureAwait(false);
        string passphrase = await this.keyProvider.GetPassphraseAsync(cancellationToken).ConfigureAwait(false);

        DateTimeOffset expiry = tenantPlan.IsTrial
            ? DateTimeOffset.UtcNow.Add(TenantPlan.TrialDuration)
            : DateTimeOffset.UtcNow.AddYears(1);

        var baseFeatures = new Dictionary<string, string>
        {
            [LicenseFeatureKey.AccessPointsMax] = tenantPlan.MaxAccessPointsPerLocation?.ToString() ?? "unlimited",
            [LicenseFeatureKey.DevicesMax] = tenantPlan.MaxDevicesPerLocation?.ToString() ?? "unlimited",
            [LicenseFeatureKey.ProductsMax] = tenantPlan.MaxProductsPerLocation?.ToString() ?? "unlimited",
            [LicenseFeatureKey.MaxLocations] = tenantPlan.MaxLocations.ToString(),
            [LicenseFeatureKey.SupportsCustomBranding] = tenantPlan.SupportsCustomBranding.ToString(),
            [LicenseFeatureKey.SupportsAnalytics] = tenantPlan.SupportsAnalytics.ToString(),
        };

        foreach (KeyValuePair<string, string> kv in featureOverrides)
        {
            baseFeatures[kv.Key] = kv.Value;
        }

        var license = Standard.Licensing.License.New()
            .WithUniqueIdentifier(Guid.NewGuid())
            .As(LicenseType.Standard)
            .ExpiresAt(expiry.UtcDateTime)
            .LicensedTo(tenantId, string.Empty)
            .WithProductFeatures(baseFeatures)
            .WithAdditionalAttributes(new Dictionary<string, string>
            {
                [LicenseFeatureKey.TenantId] = tenantId,
                [LicenseFeatureKey.LocationId] = locationId ?? "tenant-level",
                [LicenseFeatureKey.Plan] = plan,
                [LicenseFeatureKey.PaymentScope] = paymentScope,
                [LicenseFeatureKey.PaymentMethodId] = paymentMethodId ?? string.Empty,
            })
            .CreateAndSignWithPrivateKey(privateKey, passphrase);

        return license.ToString();
    }
}
