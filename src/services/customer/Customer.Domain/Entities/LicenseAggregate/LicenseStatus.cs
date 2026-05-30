// <copyright file="LicenseStatus.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Ardalis.SmartEnum;

namespace Customer.Domain.Entities.LicenseAggregate;

/// <summary>
/// Represents the status of a license.
/// </summary>
public sealed class LicenseStatus : SmartEnum<LicenseStatus>
{
    /// <summary>
    /// The license is in trial mode.
    /// </summary>
    public static readonly LicenseStatus Trial = new(nameof(Trial), 0);

    /// <summary>
    /// The license is active and fully operational.
    /// </summary>
    public static readonly LicenseStatus Active = new(nameof(Active), 1);

    /// <summary>
    /// The license has expired.
    /// </summary>
    public static readonly LicenseStatus Expired = new(nameof(Expired), 2);

    /// <summary>
    /// The license is in a grace period after expiry.
    /// </summary>
    public static readonly LicenseStatus Grace = new(nameof(Grace), 3);

    /// <summary>
    /// The license has been revoked.
    /// </summary>
    public static readonly LicenseStatus Revoked = new(nameof(Revoked), 4);

    /// <summary>
    /// The license has been superseded by a newer license (e.g., after a plan upgrade or limit increase).
    /// </summary>
    public static readonly LicenseStatus Superseded = new(nameof(Superseded), 5);

    private LicenseStatus(string name, int value) : base(name, value)
    {
    }

    /// <summary>
    /// Gets a value indicating whether the license status allows normal usage.
    /// </summary>
    public bool IsUsable => this == Active || this == Trial;

    /// <summary>
    /// Gets a value indicating whether the license is in an expired or unusable state.
    /// </summary>
    public bool IsExpired => this == Expired || this == Grace || this == Revoked;
}
