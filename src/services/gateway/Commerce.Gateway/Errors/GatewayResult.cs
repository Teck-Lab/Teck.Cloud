using Teck.Cloud.Commerce.Gateway.Models;

namespace Teck.Cloud.Commerce.Gateway.Errors;

/// <summary>An edge outcome: a projection or one sanitized published problem.</summary>
/// <typeparam name="T">The edge projection.</typeparam>
public sealed class GatewayResult<T>
    where T : class
{
    internal GatewayResult(T? value, CommerceProblemView? problem)
    {
        Value = value;
        Problem = problem;
    }

    /// <summary>Gets the successful value.</summary>
    public T? Value { get; }

    /// <summary>Gets the sanitized failure.</summary>
    public CommerceProblemView? Problem { get; }

    /// <summary>Gets a value indicating whether the outcome is successful.</summary>
    public bool IsSuccess => Problem is null;
}
