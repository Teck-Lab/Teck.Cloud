using Grpc.Core;
using Teck.Cloud.Commerce.Contracts;

namespace Commerce.IntegrationTests.Gateway;

/// <summary>Maps scripted outcomes onto gRPC trailing metadata and transport fault behavior.</summary>
internal static class FakeGrpcReplies
{
    internal static IReadOnlyList<KeyValuePair<string, string>> Capture(ServerCallContext context) =>
        [.. context.RequestHeaders.Select(entry => new KeyValuePair<string, string>(entry.Key, entry.Value))];

    internal static void AddTrailers(ServerCallContext context, IReadOnlyList<string> trailers)
    {
        foreach (var value in trailers)
        {
            context.ResponseTrailers.Add(CommerceRpc.ErrorTrailer, value);
        }
    }

    internal static RpcException Failure(DependencyFailure failure, ServerCallContext context)
    {
        if (failure.AbortTransport)
        {
            context.GetHttpContext().Abort();
        }

        var trailers = new Metadata();
        foreach (var value in failure.Trailers)
        {
            trailers.Add(CommerceRpc.ErrorTrailer, value);
        }

        return new RpcException(new Status(failure.Status, "sanitized fake dependency failure"), trailers);
    }
}
