namespace SharedKernel.Secrets;

/// <summary>
/// Configuration options for HashiCorp Vault integration.
/// </summary>
public sealed class VaultOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Vault";

    /// <summary>
    /// Vault server address (e.g., https://vault.example.com:8200).
    /// </summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>
    /// Authentication method (Token, AppRole, Kubernetes, UserPass, etc.).
    /// </summary>
    public VaultAuthMethod AuthMethod { get; init; } = VaultAuthMethod.Token;

    /// <summary>
    /// Token for token-based authentication.
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// Role ID for AppRole authentication.
    /// </summary>
    public string? RoleId { get; init; }

    /// <summary>
    /// Secret ID for AppRole authentication.
    /// </summary>
    public string? SecretId { get; init; }

    /// <summary>
    /// Kubernetes role for Kubernetes authentication.
    /// </summary>
    public string? KubernetesRole { get; init; }

    /// <summary>
    /// Username for UserPass authentication.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// Password for UserPass authentication.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// Path to Kubernetes service account token file.
    /// </summary>
    public string? KubernetesTokenPath { get; init; } = "/var/run/secrets/kubernetes.io/serviceaccount/token";

    /// <summary>
    /// Mount point for the secrets engine (default: "secret").
    /// </summary>
    public string MountPoint { get; init; } = "secret";

    /// <summary>
    /// Base path for database credentials in Vault.
    /// </summary>
    public string DatabaseSecretsPath { get; init; } = "database";

    /// <summary>
    /// Vault namespace (for enterprise Vault namespaces). If not set, no namespace header will be sent.
    /// </summary>
    public string? Namespace { get; init; }

    /// <summary>
    /// Cache duration for secrets in minutes (default: 5 minutes).
    /// </summary>
    public int CacheDurationMinutes { get; init; } = 5;

    /// <summary>
    /// Enable automatic token renewal.
    /// </summary>
    public bool EnableTokenRenewal { get; init; } = true;

    /// <summary>
    /// Timeout for Vault operations in seconds.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 30;
}

/// <summary>
/// Supported Vault authentication methods.
/// </summary>
public enum VaultAuthMethod
{
    /// <summary>
    /// Token-based authentication.
    /// </summary>
    Token,

    /// <summary>
    /// AppRole authentication (recommended for applications).
    /// </summary>
    AppRole,

    /// <summary>
    /// Kubernetes authentication (recommended for K8s deployments).
    /// </summary>
    Kubernetes,

    /// <summary>
    /// Username/password authentication (userpass).
    /// </summary>
    UserPass,
}
