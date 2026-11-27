namespace Commerce.IntegrationTests.Gateway;

/// <summary>Customer HTTP surface shared by the active-license regression.</summary>
public enum GatewaySurface
{
    /// <summary>GET /v1/commerce/orders/{orderId}.</summary>
    Detail = 0,

    /// <summary>GET /v1/commerce/orders with an original opaque cursor.</summary>
    List = 1,

    /// <summary>POST /v1/commerce/orders same-intent replay.</summary>
    Replay = 2,
}
