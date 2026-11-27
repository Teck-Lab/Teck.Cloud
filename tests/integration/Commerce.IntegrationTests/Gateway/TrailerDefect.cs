namespace Commerce.IntegrationTests.Gateway;

/// <summary>Deliberately malformed initial dependency error trailers.</summary>
public enum TrailerDefect
{
    /// <summary>Non-OK response with no error trailer.</summary>
    Missing = 0,

    /// <summary>Non-OK response with the same trailer twice.</summary>
    Duplicate = 1,

    /// <summary>Trailer value whose frozen gRPC status differs from the observed status.</summary>
    StatusMismatch = 2,

    /// <summary>Well-formed ASCII value that is not a known stable code.</summary>
    UnknownCode = 3,

    /// <summary>Value that violates the lowercase ASCII pattern.</summary>
    Malformed = 4,
}
