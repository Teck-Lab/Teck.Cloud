using ErrorOr;

namespace Teck.Cloud.Commerce.Orders.Domain.Common;

/// <summary>Positive minor-unit money with an uppercase ISO 4217 currency.</summary>
public readonly record struct Money
{
    private Money(long amountMinor, string currency)
    {
        AmountMinor = amountMinor;
        Currency = currency;
    }

    /// <summary>Gets the positive amount in minor units.</summary>
    public long AmountMinor { get; }

    /// <summary>Gets the uppercase ISO 4217 currency code.</summary>
    public string Currency { get; }

    /// <summary>Creates validated money.</summary>
    /// <param name="amountMinor">The amount in minor units.</param>
    /// <param name="currency">The uppercase ISO 4217 currency code.</param>
    /// <returns>The validated money, or a validation error.</returns>
    public static ErrorOr<Money> Create(long amountMinor, string currency) =>
        (amountMinor is > 0 and <= 9007199254740991L, IsValidCurrency(currency)) switch
        {
            (false, _) => Error.Validation("invalid_request", "The amount must be a positive JavaScript-safe integer."),
            (true, false) => Error.Validation("invalid_request", "The currency must be a three-letter uppercase ISO 4217 code."),
            (true, true) => new Money(amountMinor, currency),
        };

    private static bool IsValidCurrency(string currency) =>
        !string.IsNullOrWhiteSpace(currency)
        && currency.Length == 3
        && currency.All(static character => character is >= 'A' and <= 'Z');
}
