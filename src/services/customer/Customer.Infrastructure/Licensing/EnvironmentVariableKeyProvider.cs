// <copyright file="EnvironmentVariableKeyProvider.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Infrastructure.Licensing.Abstractions;

namespace Customer.Infrastructure.Licensing;

/// <summary>
/// Provides license keys from environment variables.
/// Phase 1 implementation — can be replaced with VaultKeyProvider for production.
/// </summary>
public sealed class EnvironmentVariableKeyProvider : ILicenseKeyProvider
{
    private const string PrivateKeyEnvVar = "LICENSE_PRIVATE_KEY";
    private const string PublicKeyEnvVar = "LICENSE_PUBLIC_KEY";

    private const string PassphraseEnvVar = "LICENSE_KEY_PASSPHRASE";

    /// <inheritdoc/>
    public Task<string> GetPrivateKeyAsync(CancellationToken cancellationToken)
    {
        string? privateKey = Environment.GetEnvironmentVariable(PrivateKeyEnvVar);

        if (string.IsNullOrWhiteSpace(privateKey))
        {
            throw new InvalidOperationException(
                $"License private key not found in environment variable '{PrivateKeyEnvVar}'. " +
                "Set this variable to the base64-encoded encrypted private key.");
        }

        return Task.FromResult(privateKey);
    }

    /// <inheritdoc/>
    public Task<string> GetPublicKeyAsync(CancellationToken cancellationToken)
    {
        string? publicKey = Environment.GetEnvironmentVariable(PublicKeyEnvVar);

        if (string.IsNullOrWhiteSpace(publicKey))
        {
            throw new InvalidOperationException(
                $"License public key not found in environment variable '{PublicKeyEnvVar}'. " +
                "Set this variable to the public key for license validation.");
        }

        return Task.FromResult(publicKey);
    }

    /// <inheritdoc/>
    public Task<string> GetPassphraseAsync(CancellationToken cancellationToken)
    {
        string? passphrase = Environment.GetEnvironmentVariable(PassphraseEnvVar);

        if (string.IsNullOrWhiteSpace(passphrase))
        {
            throw new InvalidOperationException(
                $"License key passphrase not found in environment variable '{PassphraseEnvVar}'. " +
                "Set this variable to the passphrase for the encrypted private key.");
        }

        return Task.FromResult(passphrase);
    }
}
