using Grpc.Core;
using Teck.Cloud.Commerce.Licensing.V1;
using LicensingService = Teck.Cloud.Commerce.Licensing.V1.Licensing;

namespace Commerce.IntegrationTests.Gateway;

/// <summary>Fake Licensing gRPC service that records the exact binding and desired generation.</summary>
/// <param name="script">Scripted Licensing replies served to the customer gateway.</param>
/// <param name="recorder">Recorder that captures each gateway call.</param>
internal sealed class FakeLicensingService(FakeCommerceScript script, CommerceCallRecorder recorder) : LicensingService.LicensingBase
{
    /// <inheritdoc/>
    public override Task<GetCurrentLicenseResponse> GetCurrentLicense(GetCurrentLicenseRequest request, ServerCallContext context)
    {
        recorder.Record(new RecordedCommerceCall
        {
            Method = "GetCurrentLicense",
            TenantId = request.TenantId,
            OrderId = request.OrderId,
            SubscriptionId = request.SubscriptionId,
            DesiredRevision = request.DesiredRevision,
            Metadata = FakeGrpcReplies.Capture(context),
        });
        return script.NextLicense(request.OrderId, request.DesiredRevision) switch
        {
            LicenseReply.Success success => SuccessAsync(context, success),
            LicenseReply.Failed failed => Task.FromException<GetCurrentLicenseResponse>(FakeGrpcReplies.Failure(failed.Failure, context)),
            _ => throw new InvalidOperationException("Unknown license reply."),
        };
    }

    private static Task<GetCurrentLicenseResponse> SuccessAsync(ServerCallContext context, LicenseReply.Success success)
    {
        FakeGrpcReplies.AddTrailers(context, success.Trailers);
        return Task.FromResult(success.Envelope is null
            ? new GetCurrentLicenseResponse()
            : new GetCurrentLicenseResponse { License = success.Envelope });
    }
}
