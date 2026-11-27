namespace Teck.Cloud.Commerce.Orders.Host.Composition;

/// <summary>Explicit gRPC and HTTP listen endpoints.</summary>
public sealed class OrdersListenOptions
{
    /// <summary>Gets or sets the cleartext HTTP/2 gRPC URL.</summary>
    public string GrpcUrl { get; set; } = $"{Uri.UriSchemeHttp}://0.0.0.0:8080";

    /// <summary>Gets or sets the cleartext HTTP/1 webhook and health URL.</summary>
    public string HttpUrl { get; set; } = $"{Uri.UriSchemeHttp}://0.0.0.0:8081";
}
