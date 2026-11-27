namespace Teck.Cloud.Commerce.Orders.Host.Composition;

/// <summary>Trusted identity, audience and directory-reader configuration.</summary>
public sealed class OrdersIdentityOptions
{
    /// <summary>Gets or sets the Keycloak base URL.</summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the realm.</summary>
    public string Realm { get; set; } = string.Empty;

    /// <summary>Gets or sets the canonical public issuer.</summary>
    public string? PublicIssuer { get; set; }

    /// <summary>Gets or sets the transport boundary name.</summary>
    public string Transport { get; set; } = "Https";

    /// <summary>Gets or sets this service's exact token audience.</summary>
    public string Audience { get; set; } = "commerce-orders";

    /// <summary>Gets or sets the realm-local tenant-reader client id.</summary>
    public string TenantReaderClientId { get; set; } = "commerce-orders-tenant-reader";

    /// <summary>Gets or sets the realm-local tenant-reader client secret.</summary>
    public string TenantReaderClientSecret { get; set; } = string.Empty;
}
