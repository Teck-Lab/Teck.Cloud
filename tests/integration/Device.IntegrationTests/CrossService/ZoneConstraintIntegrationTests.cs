// <copyright file="ZoneConstraintIntegrationTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Assignments.Abstractions;
using Device.Application.Assignments.Features.ApplyDeviceAssignment.V1;
using Device.Application.DeviceDefinitions.ReadModels;
using Device.Application.DeviceLayouts.ReadModels;
using Device.Application.Displays.Abstractions;
using Device.Domain.Entities.DeviceDefinitionAggregate;
using Device.Domain.Entities.DisplayAggregate;
using Device.Infrastructure.Persistence;
using Device.Infrastructure.Persistence.Repositories.Read;
using Device.IntegrationTests.TestSupport;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SharedKernel.Core.Database;
using Shouldly;

namespace Device.IntegrationTests.CrossService;

[Collection("SharedDeviceTestcontainers")]
public sealed class ZoneConstraintIntegrationTests : IAsyncLifetime
{
    private readonly SharedDeviceTestcontainersFixture _fixture;
    private DeviceReadDbContext? _dbContext;
    private IDeviceDefinitionReadRepository? _displayLayoutRepository;

    public ZoneConstraintIntegrationTests(SharedDeviceTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var options = new DbContextOptionsBuilder<DeviceReadDbContext>()
            .UseNpgsql(_fixture.DbContainer!.GetConnectionString())
            .Options;

        var tenantAccessor = new FixedTenantContextAccessor();
        _dbContext = new DeviceReadDbContext(options, tenantAccessor);

        await _dbContext.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        await _dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var factory = new TestDbContextFactory(_fixture.DbContainer!.GetConnectionString(), tenantAccessor);
        _displayLayoutRepository = new DbDisplayLayoutContextRepository(factory);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
        {
            await _dbContext.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ApplyDeviceAssignment_ShouldReturnZoneCountExceeded_WhenZonesExceedMaxZoneCount()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Arrange — seed a definition, layout (maxZoneCount=2), and a display pointing at that layout.
        var definitionId = Guid.NewGuid();
        var layoutId = Guid.NewGuid();

        _dbContext!.DeviceDefinitions.Add(new DeviceDefinitionReadModel
        {
            Id = definitionId,
            ModelId = "ZC-DEF-001",
            Name = "Zone Constraint Definition",
            EslProvider = "Hanshow",
            SupportedColors = (int)DisplayInkColor.Black,
        });

        _dbContext.DeviceLayouts.Add(new DeviceLayoutReadModel
        {
            Id = layoutId,
            DeviceDefinitionId = definitionId,
            Name = "Two-Zone Layout",
            MaxZoneCount = 2,
        });

        var displayResult = Display.Create(
            shortSerial: "AA-BB-CC-DD",
            locationNodeId: "zone-a",
            deviceDefinitionId: definitionId,
            deviceLayoutId: layoutId);

        displayResult.IsError.ShouldBeFalse();
        _dbContext.Displays.Add(displayResult.Value);

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var displayWriteRepository = Substitute.For<IDisplayWriteRepository>();
        displayWriteRepository
            .FindByIdAsync(displayResult.Value.Id, Arg.Any<CancellationToken>())
            .Returns(displayResult.Value);

        var handler = new ApplyDeviceAssignmentCommandHandler(
            _displayLayoutRepository!,
            displayWriteRepository,
            Substitute.For<IDisplayAssignmentWriteRepository>(),
            Substitute.For<ILocationTemplateContextRunner>(),
            Substitute.For<IProductSnapshotRunner>(),
            Substitute.For<ILabelRenderJobRunner>(),
            Substitute.For<IUnitOfWork>());

        var command = new ApplyDeviceAssignmentCommand(
            DeviceId: displayResult.Value.Id.ToString(),
            LocationNodeId: "zone-a",
            TemplateId: null,
            Zones:
            [
                new ApplyDeviceAssignmentZoneRequest { ZoneIndex = 1, ProductId = Guid.NewGuid().ToString() },
                new ApplyDeviceAssignmentZoneRequest { ZoneIndex = 2, ProductId = Guid.NewGuid().ToString() },
                new ApplyDeviceAssignmentZoneRequest { ZoneIndex = 3, ProductId = Guid.NewGuid().ToString() },
            ]);

        // Act
        ErrorOr<ApplyDeviceAssignmentResponse> result = await handler.Handle(
            command,
            TestContext.Current.CancellationToken);

        // Assert — 3 zones for a layout that allows 2 should be rejected
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Device.ZoneCountExceeded");
    }

    [Fact]
    public async Task ApplyDeviceAssignment_ShouldProceedPastZoneCheck_WhenZonesAreWithinMaxZoneCount()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Arrange
        var definitionId = Guid.NewGuid();
        var layoutId = Guid.NewGuid();

        _dbContext!.DeviceDefinitions.Add(new DeviceDefinitionReadModel
        {
            Id = definitionId,
            ModelId = "ZC-DEF-002",
            Name = "Zone Constraint Definition 2",
            EslProvider = "Hanshow",
            SupportedColors = (int)DisplayInkColor.Black,
        });

        _dbContext.DeviceLayouts.Add(new DeviceLayoutReadModel
        {
            Id = layoutId,
            DeviceDefinitionId = definitionId,
            Name = "Four-Zone Layout",
            MaxZoneCount = 4,
        });

        var displayResult = Display.Create(
            shortSerial: "EE-FF-GG-HH",
            locationNodeId: "zone-b",
            deviceDefinitionId: definitionId,
            deviceLayoutId: layoutId);

        displayResult.IsError.ShouldBeFalse();
        _dbContext.Displays.Add(displayResult.Value);

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var productSnapshots = Substitute.For<IProductSnapshotRunner>();
        productSnapshots
            .GetSnapshotsAsync(Arg.Any<string>(), Arg.Any<Guid[]>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var displayWriteRepository = Substitute.For<IDisplayWriteRepository>();
        displayWriteRepository
            .FindByIdAsync(displayResult.Value.Id, Arg.Any<CancellationToken>())
            .Returns(displayResult.Value);

        var handler = new ApplyDeviceAssignmentCommandHandler(
            _displayLayoutRepository!,
            displayWriteRepository,
            Substitute.For<IDisplayAssignmentWriteRepository>(),
            Substitute.For<ILocationTemplateContextRunner>(),
            productSnapshots,
            Substitute.For<ILabelRenderJobRunner>(),
            Substitute.For<IUnitOfWork>());

        var productId = Guid.NewGuid();
        var command = new ApplyDeviceAssignmentCommand(
            DeviceId: displayResult.Value.Id.ToString(),
            LocationNodeId: "zone-b",
            TemplateId: null,
            Zones:
            [
                new ApplyDeviceAssignmentZoneRequest { ZoneIndex = 1, ProductId = productId.ToString() },
                new ApplyDeviceAssignmentZoneRequest { ZoneIndex = 2, ProductId = productId.ToString() },
            ]);

        // Act
        ErrorOr<ApplyDeviceAssignmentResponse> result = await handler.Handle(
            command,
            TestContext.Current.CancellationToken);

        // Assert — zone count is within range, so ZoneCountExceeded is not the error
        // (the handler proceeds to product validation which fails for missing products — that's expected)
        if (result.IsError)
        {
            result.FirstError.Code.ShouldNotBe("Device.ZoneCountExceeded");
        }
    }

    private sealed class TestDbContextFactory(
        string connectionString,
        FixedTenantContextAccessor tenantAccessor) : IDbContextFactory<DeviceReadDbContext>
    {
        private readonly string connectionString = connectionString;
        private readonly FixedTenantContextAccessor tenantAccessor = tenantAccessor;

        public DeviceReadDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<DeviceReadDbContext>()
                .UseNpgsql(this.connectionString)
                .Options;

            return new DeviceReadDbContext(options, this.tenantAccessor);
        }
    }
}
