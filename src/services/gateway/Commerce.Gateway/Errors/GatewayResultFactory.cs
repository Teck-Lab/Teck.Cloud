using Teck.Cloud.Commerce.Gateway.Models;

namespace Teck.Cloud.Commerce.Gateway.Errors;

/// <summary>Creates edge outcomes without declaring static members on the generic result type.</summary>
internal static class GatewayResultFactory
{
    /// <summary>Creates a successful outcome.</summary>
    /// <typeparam name="T">The edge projection.</typeparam>
    /// <param name="value">The projection.</param>
    /// <returns>The outcome.</returns>
    public static GatewayResult<T> Ok<T>(T value)
        where T : class =>
        new(value: value, problem: null);

    /// <summary>Creates a failed outcome.</summary>
    /// <typeparam name="T">The edge projection.</typeparam>
    /// <param name="problem">The sanitized problem.</param>
    /// <returns>The outcome.</returns>
    public static GatewayResult<T> Fail<T>(CommerceProblemView problem)
        where T : class =>
        new(value: null, problem: problem);
}
