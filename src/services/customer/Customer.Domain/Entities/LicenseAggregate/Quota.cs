// <copyright file="Quota.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using SharedKernel.Core.Pricing;

namespace Customer.Domain.Entities.LicenseAggregate;

/// <summary>
/// Represents usage quota limits for a license.
/// </summary>
public sealed class Quota : IEquatable<Quota>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Quota"/> class.
    /// </summary>
    /// <param name="maxAccessPoints">Maximum access points allowed.</param>
    /// <param name="maxDevices">Maximum devices allowed.</param>
    /// <param name="maxProducts">Maximum products allowed.</param>
    /// <param name="maxLocations">Maximum locations allowed.</param>
    private Quota(int? maxAccessPoints, int? maxDevices, int? maxProducts, int? maxLocations)
    {
        this.MaxAccessPoints = maxAccessPoints;
        this.MaxDevices = maxDevices;
        this.MaxProducts = maxProducts;
        this.MaxLocations = maxLocations;
    }

    /// <summary>
    /// Gets the maximum number of access points allowed, or null for unlimited.
    /// </summary>
    public int? MaxAccessPoints { get; }

    /// <summary>
    /// Gets the maximum number of devices allowed, or null for unlimited.
    /// </summary>
    public int? MaxDevices { get; }

    /// <summary>
    /// Gets the maximum number of products allowed, or null for unlimited.
    /// </summary>
    public int? MaxProducts { get; }

    /// <summary>
    /// Gets the maximum number of locations allowed, or null for unlimited.
    /// </summary>
    public int? MaxLocations { get; }

    /// <summary>
    /// Creates a quota from a tenant plan.
    /// </summary>
    /// <param name="plan">The tenant plan.</param>
    /// <returns>The quota for the plan.</returns>
    public static Quota FromTenantPlan(TenantPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new Quota(
            plan.MaxAccessPointsPerLocation,
            plan.MaxDevicesPerLocation,
            plan.MaxProductsPerLocation,
            plan.MaxLocations);
    }

    /// <summary>
    /// Validates whether usage counts are within quota limits.
    /// </summary>
    /// <param name="usage">The current usage counts.</param>
    /// <returns>An error if quota is exceeded, otherwise success.</returns>
    public ErrorOr<Success> ValidateUsage(UsageCounts usage)
    {
        ArgumentNullException.ThrowIfNull(usage);

        List<Error> errors = [];

        if (this.MaxAccessPoints.HasValue && usage.AccessPoints > this.MaxAccessPoints.Value)
        {
            errors.Add(Error.Validation("Quota.AccessPoints", $"Access point quota exceeded: {usage.AccessPoints}/{this.MaxAccessPoints.Value}"));
        }

        if (this.MaxDevices.HasValue && usage.Devices > this.MaxDevices.Value)
        {
            errors.Add(Error.Validation("Quota.Devices", $"Device quota exceeded: {usage.Devices}/{this.MaxDevices.Value}"));
        }

        if (this.MaxProducts.HasValue && usage.Products > this.MaxProducts.Value)
        {
            errors.Add(Error.Validation("Quota.Products", $"Product quota exceeded: {usage.Products}/{this.MaxProducts.Value}"));
        }

        if (this.MaxLocations.HasValue && usage.Locations > this.MaxLocations.Value)
        {
            errors.Add(Error.Validation("Quota.Locations", $"Location quota exceeded: {usage.Locations}/{this.MaxLocations.Value}"));
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        return Result.Success;
    }

    /// <inheritdoc/>
    public bool Equals(Quota? other)
    {
        if (other is null)
        {
            return false;
        }

        return this.MaxAccessPoints == other.MaxAccessPoints
            && this.MaxDevices == other.MaxDevices
            && this.MaxProducts == other.MaxProducts
            && this.MaxLocations == other.MaxLocations;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is Quota other && this.Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(this.MaxAccessPoints, this.MaxDevices, this.MaxProducts, this.MaxLocations);
    }
}
