// <copyright file="ILicenseKeyProvider.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Infrastructure.Licensing.Abstractions;

/// <summary>
/// Provides access to the license signing and validation keys.
/// </summary>
public interface ILicenseKeyProvider
{
    /// <summary>
    /// Gets the private key for signing licenses.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The encrypted private key string.</returns>
    Task<string> GetPrivateKeyAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the public key for validating licenses.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The public key string.</returns>
    Task<string> GetPublicKeyAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the passphrase for decrypting the private key.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The passphrase string.</returns>
    Task<string> GetPassphraseAsync(CancellationToken cancellationToken);
}
