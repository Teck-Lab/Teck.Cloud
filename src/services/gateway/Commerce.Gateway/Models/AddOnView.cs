namespace Teck.Cloud.Commerce.Gateway.Models;

/// <summary>An optional catalog add-on.</summary>
/// <param name="Code">Opaque add-on code.</param>
/// <param name="DisplayName">Client display label.</param>
/// <param name="Price">Server-owned add-on price.</param>
public sealed record AddOnView(string Code, string DisplayName, MoneyView Price);
