namespace Teck.Cloud.Commerce.Gateway.Errors;

/// <summary>A dependency response that cannot be interpreted under the frozen wire contract.</summary>
public sealed class CommerceDependencyException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="CommerceDependencyException"/> class.</summary>
    public CommerceDependencyException()
        : base("The dependency response is unavailable or malformed.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CommerceDependencyException"/> class with a specified error message.</summary>
    /// <param name="message">Non-sensitive diagnostic context for logs; never returned to a client.</param>
    public CommerceDependencyException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CommerceDependencyException"/> class with a specified error message and inner exception.</summary>
    /// <param name="message">Non-sensitive diagnostic context for logs; never returned to a client.</param>
    /// <param name="innerException">The dependency failure.</param>
    public CommerceDependencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
