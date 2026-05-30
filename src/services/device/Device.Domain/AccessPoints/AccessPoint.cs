// <copyright file="AccessPoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using SharedKernel.Core.Domain;

namespace Device.Domain.AccessPoints;

/// <summary>
/// Represents an ESL access point available at a location node.
/// </summary>
public sealed class AccessPoint : BaseEntity, IAggregateRoot
{
    private AccessPoint()
    {
    }

    /// <summary>
    /// Gets the supplier serial number for this access point.
    /// </summary>
    public string SerialNumber { get; private set; } = default!;

    /// <summary>
    /// Gets the access point vendor name, for example Hanshow, SoluM, or Stub.
    /// </summary>
    public string Vendor { get; private set; } = default!;

    /// <summary>
    /// Gets the location node this access point is assigned to.
    /// </summary>
    public string LocationNodeId { get; private set; } = default!;

    /// <summary>
    /// Gets the current operational status.
    /// </summary>
    public AccessPointStatus Status { get; private set; }

    /// <summary>
    /// Gets the maximum number of displays this access point can support.
    /// </summary>
    public int MaxCapacity { get; private set; }

    /// <summary>
    /// Gets the number of displays currently assigned to this access point.
    /// </summary>
    public int CurrentLoad { get; private set; }

    /// <summary>
    /// Creates a new access point.
    /// </summary>
    /// <param name="serialNumber">Supplier serial number.</param>
    /// <param name="vendor">Vendor name.</param>
    /// <param name="locationNodeId">Location node identifier.</param>
    /// <param name="maxCapacity">Maximum supported display count.</param>
    /// <returns>The created access point or validation errors.</returns>
    public static ErrorOr<AccessPoint> Create(
        string serialNumber,
        string vendor,
        string locationNodeId,
        int maxCapacity)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            return Error.Validation("AccessPoint.SerialNumberRequired", "Serial number is required.");
        }

        if (string.IsNullOrWhiteSpace(vendor))
        {
            return Error.Validation("AccessPoint.VendorRequired", "Vendor is required.");
        }

        if (string.IsNullOrWhiteSpace(locationNodeId))
        {
            return Error.Validation("AccessPoint.LocationNodeIdRequired", "Location node ID is required.");
        }

        if (maxCapacity <= 0)
        {
            return Error.Validation("AccessPoint.MaxCapacityInvalid", "Max capacity must be greater than zero.");
        }

        AccessPoint accessPoint = new()
        {
            SerialNumber = serialNumber.Trim().ToUpperInvariant(),
            Vendor = vendor.Trim(),
            LocationNodeId = locationNodeId.Trim(),
            Status = AccessPointStatus.Online,
            MaxCapacity = maxCapacity,
            CurrentLoad = 0,
        };

        return accessPoint;
    }

    /// <summary>
    /// Sets the current operational status.
    /// </summary>
    /// <param name="status">The new status.</param>
    public void SetStatus(AccessPointStatus status) => Status = status;

    /// <summary>
    /// Increments the current load if capacity is available.
    /// </summary>
    /// <returns>Success or a capacity conflict.</returns>
    public ErrorOr<Success> IncrementLoad()
    {
        if (CurrentLoad >= MaxCapacity)
        {
            return Error.Conflict("AccessPoint.CapacityExceeded", "Access point capacity has been reached.");
        }

        CurrentLoad++;
        return Result.Success;
    }

    /// <summary>
    /// Decrements the current load without going below zero.
    /// </summary>
    public void DecrementLoad()
    {
        if (CurrentLoad > 0)
        {
            CurrentLoad--;
        }
    }
}
