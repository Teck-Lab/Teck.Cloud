namespace Commerce.IntegrationTests.Gateway;

/// <summary>Scripted non-OK result of the confirmation GetCurrentLicense call.</summary>
public enum ConfirmationFailure
{
    /// <summary>PERMISSION_DENIED carrying the frozen forbidden trailer.</summary>
    Forbidden = 0,

    /// <summary>NOT_FOUND carrying the frozen not_found trailer.</summary>
    NotFound = 1,

    /// <summary>A broken gRPC transport with no trailers.</summary>
    Transport = 2,
}
