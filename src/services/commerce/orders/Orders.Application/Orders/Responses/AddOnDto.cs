namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>Wire add-on with its price.</summary>
/// <param name="Code">The add-on code.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Price">The monthly price.</param>
public sealed record AddOnDto(string Code, string DisplayName, MoneyDto Price);
