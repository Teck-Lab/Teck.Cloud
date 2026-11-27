namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>Checkout response.</summary>
/// <param name="Order">The current customer order projection.</param>
/// <param name="CheckoutUrl">The usable provider checkout URL, or null while unavailable.</param>
public sealed record CheckoutResultDto(CustomerOrderDto Order, string? CheckoutUrl);
