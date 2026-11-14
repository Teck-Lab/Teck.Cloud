namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Detects accidental reflection over unregistered exception properties.</summary>
public sealed class UnknownDiagnosticException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="UnknownDiagnosticException"/> class.</summary>
    public UnknownDiagnosticException()
        : base("diagnostic-probe")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="UnknownDiagnosticException"/> class.</summary>
    /// <param name="message">Diagnostic message.</param>
    public UnknownDiagnosticException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="UnknownDiagnosticException"/> class.</summary>
    /// <param name="message">Diagnostic message.</param>
    /// <param name="innerException">Cause.</param>
    public UnknownDiagnosticException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Gets a value indicating whether the custom getter was evaluated.</summary>
    public bool DetailRead { get; private set; }

    /// <summary>Gets data which must never be visited through reflection.</summary>
    public string CustomDetail
    {
        get
        {
            DetailRead = true;
            return "never-visit-custom-detail";
        }
    }
}
