namespace Commerce.IntegrationTests.Gateway;

/// <summary>Malformed Licensing success responses.</summary>
public enum MalformedLicenseSuccess
{
    /// <summary>Clean OK with no envelope.</summary>
    EmptyEnvelope = 0,

    /// <summary>OK carrying an error trailer.</summary>
    TraileredOk = 1,
}
