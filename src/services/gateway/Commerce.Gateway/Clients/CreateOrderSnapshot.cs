namespace Teck.Cloud.Commerce.Gateway.Clients;

/// <summary>The bound order and current checkout availability from Orders.</summary>
/// <param name="Order">The bound order snapshot.</param>
/// <param name="CheckoutUrl">Current provider URL, or null when not payable.</param>
public sealed record CreateOrderSnapshot(CustomerOrderSnapshot Order, string? CheckoutUrl);
