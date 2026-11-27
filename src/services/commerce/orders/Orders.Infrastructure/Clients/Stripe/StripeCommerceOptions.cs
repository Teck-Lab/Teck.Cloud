using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Clients.Stripe;

/// <summary>
/// The immutable Stripe adapter configuration snapshot; absent values leave provider operations unavailable.
/// </summary>
internal sealed class StripeCommerceOptions
{
    /// <summary>The section path that supplies the Stripe adapter settings.</summary>
    public const string SectionPath = "CommerceOrders:Stripe";

    /// <summary>The default provider API base URL.</summary>
    public const string DefaultApiBase = "https://api.stripe.com";

    /// <summary>The default hosted checkout origin.</summary>
    public const string DefaultCheckoutOrigin = "https://checkout.stripe.com";

    /// <summary>The default webhook signature tolerance in seconds.</summary>
    public const long DefaultWebhookToleranceSeconds = 300;

    /// <summary>Gets the server-held restricted or secret API key.</summary>
    public string? SecretKey { get; init; }

    /// <summary>Gets the endpoint signing secret used only for raw webhook verification.</summary>
    public string? WebhookSecret { get; init; }

    /// <summary>Gets the provider account identifier used to verify webhook account/mode.</summary>
    public string? AccountId { get; init; }

    /// <summary>Gets the configured provider mode text, test or live.</summary>
    public string? Mode { get; init; }

    /// <summary>Gets the optional provider API base URL; test-only fixtures may point this at a local server.</summary>
    public string? ApiBaseUrl { get; init; }

    /// <summary>Gets the exact allowed hosted checkout origin.</summary>
    public string? CheckoutOrigin { get; init; }

    /// <summary>Gets the absolute HTTPS success URL template containing a literal {orderId} token.</summary>
    public string? SuccessUrl { get; init; }

    /// <summary>Gets the absolute HTTPS cancel URL template containing a literal {orderId} token.</summary>
    public string? CancelUrl { get; init; }

    /// <summary>Gets the webhook signature tolerance in seconds.</summary>
    public long WebhookToleranceSeconds { get; init; } = DefaultWebhookToleranceSeconds;

    /// <summary>Gets the configured recurring provider price identifiers keyed by catalog offer or add-on code.</summary>
    public IReadOnlyDictionary<string, string> PriceIds { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Reads the adapter configuration without throwing for absent or invalid billing values.</summary>
    /// <param name="configuration">The application configuration source.</param>
    /// <returns>The immutable configuration snapshot.</returns>
    public static StripeCommerceOptions Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SectionPath);
        var priceIds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var child in section.GetSection("PriceIds").GetChildren())
        {
            priceIds[child.Key] = child.Value ?? string.Empty;
        }

        return new StripeCommerceOptions
        {
            SecretKey = section["SecretKey"],
            WebhookSecret = section["WebhookSecret"],
            AccountId = section["AccountId"],
            Mode = section["Mode"],
            ApiBaseUrl = section["ApiBaseUrl"],
            CheckoutOrigin = section["CheckoutOrigin"],
            SuccessUrl = section["SuccessUrl"],
            CancelUrl = section["CancelUrl"],
            WebhookToleranceSeconds = ParseTolerance(section["WebhookToleranceSeconds"]),
            PriceIds = priceIds,
        };
    }

    private static long ParseTolerance(string? value)
    {
        return !long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tolerance) || tolerance <= 0
            ? DefaultWebhookToleranceSeconds
            : Math.Min(tolerance, 3600);
    }
}
