// <copyright file="ApplyDeviceAssignmentChainIntegrationTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using Device.Application.Assignments.Features.ApplyDeviceAssignment.V1;
using Device.Domain.AccessPoints;
using Device.Domain.Entities.DisplayAggregate;
using Device.Domain.Entities.DisplayAssignmentAggregate;
using Device.Infrastructure.Persistence;
using Device.IntegrationTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Events;
using Shouldly;

namespace Device.IntegrationTests.Endpoints.Assignments;

[Collection("SharedDeviceTestcontainers")]
public sealed class ApplyDeviceAssignmentChainIntegrationTests
{
    private const string TenantId = "test-tenant";

    private readonly SharedDeviceTestcontainersFixture fixture;

    public ApplyDeviceAssignmentChainIntegrationTests(SharedDeviceTestcontainersFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task ApplyAssignment_ShouldTransitionPendingToRenderedToDelivered_WhenRenderAndDispatchEventsArrive()
    {
        if (!this.fixture.IsAvailable)
        {
            return;
        }

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid customProductId = Guid.NewGuid();
        Guid expectedRenderJobId = Guid.NewGuid();

        await using TestRemoteProductHost productHost = await TestRemoteProductHost.StartAsync(
            customProductId,
            "Chain Espresso Capsules",
            cancellationToken);

        await using TestRemoteLabelHost labelHost = await TestRemoteLabelHost.StartAsync(
            expectedRenderJobId,
            "queued",
            cancellationToken);

        string postgresConn = this.fixture.DbContainer!.GetConnectionString();
        string rabbitConn = this.fixture.RabbitContainer!.GetConnectionString();

        await using TestDeviceApiHostWithMessaging deviceHost = await TestDeviceApiHostWithMessaging.StartAsync(
            postgresConn,
            rabbitConn,
            productHost.BaseAddress,
            labelHost.BaseAddress,
            cancellationToken);

        Guid displayId = await SeedDisplayAsync(postgresConn, cancellationToken);

        deviceHost.Client.DefaultRequestHeaders.Add("X-TenantId", TenantId);

        var request = new ApplyDeviceAssignmentRequest
        {
            DeviceId = displayId.ToString(),
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
            cancellationToken);

        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"Body: {responseBody}");

        ApplyDeviceAssignmentResponse? payload = await response.Content.ReadFromJsonAsync<ApplyDeviceAssignmentResponse>(
            cancellationToken: cancellationToken);
        payload.ShouldNotBeNull();
        payload.RenderJobId.ShouldNotBe(Guid.Empty);

        payload.AssignmentId.ShouldNotBe(Guid.Empty);
        Guid assignmentId = payload.AssignmentId;
        Guid renderJobId = await GetPersistedRenderJobIdAsync(postgresConn, assignmentId, cancellationToken);

        await deviceHost.MessageBus.PublishAsync(new RenderJobCompletedIntegrationEvent(
            renderJobId,
            displayId,
            new Uri("file:///rendered.png")));

        await WaitForStatusAsync(
            postgresConn,
            assignmentId,
            DisplayAssignmentStatus.Rendered,
            TimeSpan.FromSeconds(10),
            cancellationToken);

        await deviceHost.MessageBus.PublishAsync(new EslDispatchCompletedIntegrationEvent(
            assignmentId,
            displayId,
            "test-provider",
            DateTimeOffset.UtcNow,
            accessPointSerial: null));

        await WaitForStatusAsync(
            postgresConn,
            assignmentId,
            DisplayAssignmentStatus.Delivered,
            TimeSpan.FromSeconds(10),
            cancellationToken);
    }

    private static async Task<Guid> SeedDisplayAsync(string postgresConnectionString, CancellationToken cancellationToken)
    {
        DbContextOptions<DeviceWriteDbContext> options = new DbContextOptionsBuilder<DeviceWriteDbContext>()
            .UseNpgsql(postgresConnectionString)
            .Options;

        await using DeviceWriteDbContext dbContext = new(options, new FixedTenantContextAccessor(TenantId));
        ErrorOr.ErrorOr<Display> created = Display.Create(
            shortSerial: "C7-A1-N0-01",
            locationNodeId: "zone-b",
            deviceDefinitionId: null);

        Display display = created.Value;
        ErrorOr.ErrorOr<AccessPoint> accessPoint = AccessPoint.Create(
            serialNumber: "AP-STUB-001",
            vendor: "Stub",
            locationNodeId: "zone-b",
            maxCapacity: 10);

        await dbContext.Set<Display>().AddAsync(display, cancellationToken).ConfigureAwait(false);
        await dbContext.Set<AccessPoint>().AddAsync(accessPoint.Value, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return display.Id;
    }

    private static async Task<Guid> GetPersistedRenderJobIdAsync(
        string postgresConnectionString,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        DbContextOptions<DeviceWriteDbContext> options = new DbContextOptionsBuilder<DeviceWriteDbContext>()
            .UseNpgsql(postgresConnectionString)
            .Options;

        await using DeviceWriteDbContext dbContext = new(options, new FixedTenantContextAccessor(TenantId));

        DisplayAssignment? assignment = await dbContext.Set<DisplayAssignment>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assignmentId, cancellationToken)
            .ConfigureAwait(false);

        assignment.ShouldNotBeNull("Apply endpoint did not persist a DisplayAssignment for the expected assignment id.");
        return assignment.RenderJobId;
    }

    private static async Task WaitForStatusAsync(
        string postgresConnectionString,
        Guid assignmentId,
        DisplayAssignmentStatus expectedStatus,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DbContextOptions<DeviceWriteDbContext> options = new DbContextOptionsBuilder<DeviceWriteDbContext>()
            .UseNpgsql(postgresConnectionString)
            .Options;

        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        DisplayAssignmentStatus? lastSeen = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            await using DeviceWriteDbContext dbContext = new(options, new FixedTenantContextAccessor(TenantId));
            DisplayAssignment? assignment = await dbContext.Set<DisplayAssignment>()
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == assignmentId, cancellationToken)
                .ConfigureAwait(false);

            if (assignment is not null)
            {
                lastSeen = assignment.Status;
                if (assignment.Status == expectedStatus)
                {
                    return;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
        }

        throw new Xunit.Sdk.XunitException(
            $"Timed out waiting for DisplayAssignment {assignmentId} to reach status {expectedStatus}. Last observed: {lastSeen?.ToString() ?? "<not found>"}.");
    }
}
