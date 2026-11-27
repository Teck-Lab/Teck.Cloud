using Grpc.Core;
using Teck.Cloud.Commerce.Licensing.V1;

namespace Commerce.IntegrationTests.Gateway;

/// <summary>Scripted Licensing envelope outcome, including absent and trailered OK responses.</summary>
internal abstract record LicenseReply
{
    internal static LicenseReply Ok(LicenseEnvelope envelope, params string[] trailers) => new Success(envelope, trailers);

    internal static LicenseReply Empty() => new Success(Envelope: null, Trailers: []);

    internal static LicenseReply Error(StatusCode status, params string[] trailers) => new Failed(DependencyFailure.Trailered(status, trailers));

    internal static LicenseReply Mute(StatusCode status) => new Failed(DependencyFailure.MissingTrailer(status));

    internal static LicenseReply TransportFailure() => new Failed(DependencyFailure.Transport());

    internal sealed record Success(LicenseEnvelope? Envelope, IReadOnlyList<string> Trailers) : LicenseReply;

    internal sealed record Failed(DependencyFailure Failure) : LicenseReply;
}
