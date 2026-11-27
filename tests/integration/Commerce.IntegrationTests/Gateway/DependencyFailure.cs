using Grpc.Core;

namespace Commerce.IntegrationTests.Gateway;

/// <summary>A scripted gRPC outcome including deliberately malformed trailer variants.</summary>
internal sealed record DependencyFailure
{
    internal required StatusCode Status { get; init; }

    internal IReadOnlyList<string> Trailers { get; init; } = [];

    internal bool AbortTransport { get; init; }

    internal static DependencyFailure Trailered(StatusCode status, params string[] trailers) =>
        new() { Status = status, Trailers = trailers };

    internal static DependencyFailure MissingTrailer(StatusCode status) => new() { Status = status };

    internal static DependencyFailure Transport() => new() { Status = StatusCode.Internal, AbortTransport = true };
}
