namespace Teck.Cloud.Commerce.Gateway.Models;

/// <summary>The bound order plus its current checkout URL when payable.</summary>
/// <param name="Order">Projected bound order.</param>
/// <param name="CheckoutUrl">Provider checkout URL, or null when not payable.</param>
public sealed record CreateOrderView(CustomerOrderView Order, string? CheckoutUrl);
