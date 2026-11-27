namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>Internal entitlement check result.</summary>
/// <param name="Eligible">Whether the binding and generation are currently eligible.</param>
public sealed record EntitlementCheckDto(bool Eligible);
