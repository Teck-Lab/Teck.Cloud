using ErrorOr;

namespace Teck.Cloud.Commerce.Licensing.Application.Entitlements.Ports;

/// <summary>Issues and inspects Standard.Licensing documents against the configured key ring.</summary>
public interface ILicenseDocumentService
{
    /// <summary>Gets the active signer identifier used for new documents.</summary>
    string ActiveKeyId { get; }

    /// <summary>Signs a complete document for one accepted generation.</summary>
    /// <param name="request">The exact signed payload.</param>
    /// <returns>The signed document, or a stable failure.</returns>
    ErrorOr<SignedLicenseDocument> Sign(LicenseSigningRequest request);

    /// <summary>Parses a bounded document and verifies its signature against its key identifier.</summary>
    /// <param name="document">The candidate document; untrusted input.</param>
    /// <returns>The verification outcome; malformed input is never trusted.</returns>
    LicenseDocumentInspection Inspect(string document);
}
