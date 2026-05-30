using System.Net;
using System.Net.Http.Json;
using Device.Application.Assignments.Features.ApplyDeviceAssignment.V1;
using Device.IntegrationTests.TestSupport;
using Shouldly;

namespace Device.IntegrationTests.Endpoints.Assignments;

public sealed class ApplyDeviceAssignmentRemoteIntegrationTests
{
    [Fact]
    public async Task Apply_ShouldUseRemoteProductAndLabelServices_WhenRemoteEndpointsAreConfigured()
    {
        Guid customProductId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        Guid expectedRenderJobId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        Guid deviceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        await using TestRemoteProductHost productHost = await TestRemoteProductHost.StartAsync(
            customProductId,
            "Remote Espresso Capsules",
            TestContext.Current.CancellationToken);

        await using TestRemoteLabelHost labelHost = await TestRemoteLabelHost.StartAsync(
            expectedRenderJobId,
            "remote-queued",
            TestContext.Current.CancellationToken);

        await using TestDeviceApiHost deviceHost = await TestDeviceApiHost.StartAsync(
            productHost.BaseAddress,
            labelHost.BaseAddress,
            deviceId,
            TestContext.Current.CancellationToken);

        var request = new ApplyDeviceAssignmentRequest
        {
            DeviceId = deviceId.ToString(),
            LocationNodeId = "zone-b",
            TemplateId = null,
            Zones =
            [
                new ApplyDeviceAssignmentZoneRequest
                {
                    ZoneIndex = 1,
                    ProductId = customProductId.ToString(),
                },
            ],
        };

        HttpResponseMessage response = await deviceHost.Client.PostAsJsonAsync(
            "/device/v1/Assignments/Apply",
            request,
            TestContext.Current.CancellationToken);

        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            $"Body: {responseBody}; productHostCallCount: {productHost.CallCount}; labelHostCallCount: {labelHost.CallCount}");

        ApplyDeviceAssignmentResponse? payload = await response.Content.ReadFromJsonAsync<ApplyDeviceAssignmentResponse>(
            cancellationToken: TestContext.Current.CancellationToken);

        payload.ShouldNotBeNull();
        payload.DeviceId.ShouldBe(deviceId.ToString());
        payload.ResolvedTemplateId.ShouldBe("template-zone-b");
        payload.TemplateSource.ShouldBe("Location");
        payload.ZoneCount.ShouldBe(1);
        payload.DuplicateProductsAllowed.ShouldBeTrue();
        payload.RenderJobId.ShouldBe(expectedRenderJobId);
        payload.RenderJobStatus.ShouldBe("remote-queued");

        productHost.CallCount.ShouldBe(1);
        labelHost.CallCount.ShouldBe(1);
    }
}