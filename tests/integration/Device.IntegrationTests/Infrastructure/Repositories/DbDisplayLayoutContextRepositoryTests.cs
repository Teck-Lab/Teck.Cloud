// <copyright file="DbDisplayLayoutContextRepositoryTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Assignments.Abstractions;
using Device.Domain.Entities.DeviceDefinitionAggregate;
using Device.Domain.Entities.DeviceLayoutAggregate;
using Device.Domain.Entities.DisplayAggregate;
using Device.Infrastructure.Persistence;
using Device.Infrastructure.Persistence.Repositories.Read;
using Device.IntegrationTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Teck.Cloud.IntegrationTests.Shared;

namespace Device.IntegrationTests.Infrastructure.Repositories;

[Collection("SharedTestcontainers")]
public sealed class DbDisplayLayoutContextRepositoryTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private DeviceWriteDbContext? _writeDbContext;
    private IDeviceDefinitionReadRepository? _repository;
    private string? _connectionString;

    public DbDisplayLayoutContextRepositoryTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create database using write context migrations (has all tables)
        _connectionString = await _fixture.CreateSharedTestDatabaseAsync(
            typeof(DeviceWriteDbContext),
            "Teck.Cloud.Migrations.PostgreSQL",
            TestContext.Current.CancellationToken);

        // Create write context for seeding data
        var writeOptions = new DbContextOptionsBuilder<DeviceWriteDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        var tenantAccessor = new FixedTenantContextAccessor();
        _writeDbContext = new DeviceWriteDbContext(writeOptions, tenantAccessor);

        var factory = new TestDbContextFactory(_connectionString);
        _repository = new DbDisplayLayoutContextRepository(factory);
    }

    public async ValueTask DisposeAsync()
    {
        if (_writeDbContext is not null)
        {
            await _writeDbContext.DisposeAsync();
        }

        if (_connectionString is not null)
        {
            await _fixture.TruncateAllTablesAsync(_connectionString, TestContext.Current.CancellationToken);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetLayoutContextByDisplayIdAsync_ShouldReturnContext_WhenDisplayHasLayout()
    {
        // Arrange — seed definition, layout, and a display pointing at that layout
        var definitionResult = DeviceDefinition.Create(
            modelId: "DLCR-DEF-001",
            name: "Layout Context Definition",
            eslProvider: SharedKernel.Core.Devices.EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        definitionResult.IsError.ShouldBeFalse();
        _writeDbContext!.DeviceDefinitions.Add(definitionResult.Value);

        var layoutResult = DeviceLayout.Create(
            deviceDefinitionId: definitionResult.Value.Id,
            name: "Three-Zone Layout",
            maxZoneCount: 3);

        layoutResult.IsError.ShouldBeFalse();
        _writeDbContext.DeviceLayouts.Add(layoutResult.Value);

        var displayResult = Display.Create(
            shortSerial: "DC-AB-CD-EF",
            locationNodeId: "shelf-dlcr",
            deviceDefinitionId: definitionResult.Value.Id,
            deviceLayoutId: layoutResult.Value.Id);

        displayResult.IsError.ShouldBeFalse();
        _writeDbContext.Displays.Add(displayResult.Value);

        await _writeDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        DisplayLayoutContext? context = await _repository!.GetLayoutContextByDisplayIdAsync(
            displayResult.Value.Id,
            TestContext.Current.CancellationToken);

        // Assert
        context.ShouldNotBeNull();
        context!.DisplayId.ShouldBe(displayResult.Value.Id);
        context.DeviceLayoutId.ShouldBe(layoutResult.Value.Id);
        context.MaxZoneCount.ShouldBe(3);
    }

    [Fact]
    public async Task GetLayoutContextByDisplayIdAsync_ShouldReturnNull_WhenDisplayHasNoLayout()
    {
        // Arrange — seed a display without a DeviceLayoutId
        var displayResult = Display.Create(
            shortSerial: "NO-LA-YO-UT",
            locationNodeId: "shelf-no-layout",
            deviceDefinitionId: null,
            deviceLayoutId: null);

        displayResult.IsError.ShouldBeFalse();
        _writeDbContext!.Displays.Add(displayResult.Value);

        await _writeDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        DisplayLayoutContext? context = await _repository!.GetLayoutContextByDisplayIdAsync(
            displayResult.Value.Id,
            TestContext.Current.CancellationToken);

        // Assert
        context.ShouldBeNull();
    }

    [Fact]
    public async Task GetLayoutContextByDisplayIdAsync_ShouldReturnNull_WhenDisplayDoesNotExist()
    {
        // Act
        DisplayLayoutContext? context = await _repository!.GetLayoutContextByDisplayIdAsync(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        // Assert
        context.ShouldBeNull();
    }

    /// <summary>
    /// A simple <see cref="IDbContextFactory{TContext}"/> implementation for tests.
    /// </summary>
    private sealed class TestDbContextFactory(string connectionString) : IDbContextFactory<DeviceReadDbContext>
    {
        private readonly string connectionString = connectionString;

        public DeviceReadDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<DeviceReadDbContext>()
                .UseNpgsql(this.connectionString)
                .Options;

            var tenantAccessor = new FixedTenantContextAccessor();
            return new DeviceReadDbContext(options, tenantAccessor);
        }
    }
}
