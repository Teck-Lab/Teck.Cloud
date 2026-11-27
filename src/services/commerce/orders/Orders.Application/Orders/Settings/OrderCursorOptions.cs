namespace Teck.Cloud.Commerce.Orders.Application.Orders.Settings;

/// <summary>Cursor signing configuration.</summary>
public sealed class OrderCursorOptions
{
    /// <summary>Gets or sets the HMAC signing secret; required and never derived from a default.</summary>
    public string SigningKey { get; set; } = string.Empty;
}
