// <copyright file="DbDeviceLayoutReadRepositoryTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceLayouts.Abstractions;
using Device.Domain.Entities.DeviceDefinitionAggregate;
using Device.Domain.Entities.DeviceLayoutAggregate;
using Device.Infrastructure.Persistence;
using Device.Infrastructure.Persistence.Repositories.Read;
using Device.IntegrationTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Pagination;
using Shouldly;
using Teck.Cloud.IntegrationTests.Shared;

namespace Device.IntegrationTests.Infrastructure.Repositories;

[Collection("SharedTestcontainers")]
public sealed class DbDeviceLayoutReadRepositoryTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private DeviceReadDbContext? _readDbContext;
    private DeviceWriteDbContext? _writeDbContext;
    private IDeviceLayoutReadRepository? _repository;
    private string? _connectionString;

    public DbDeviceLayoutReadRepositoryTests(SharedTestcontainersFixture fixture)
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

        _writeDbContext = new DeviceWriteDbContext(writeOptions);

        // Create read context for the repository
        var readOptions = new DbContextOptionsBuilder<DeviceReadDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        _readDbContext = new DeviceReadDbContext(readOptions);

        var factory = new TestDbContextFactory(_connectionString);
        _repository = new DbDeviceLayoutReadRepository(factory);
    }

    public async ValueTask DisposeAsync()
    {
        if (_readDbContext is not null)
        {
            await _readDbContext.DisposeAsync();
        }

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
    public async Task GetByDeviceDefinitionIdAsync_ShouldReturnLayouts_WhenLayoutsExist()
    {
        var definitionResult = DeviceDefinition.Create(
            modelId: "HS-LAYOUT-DEF-001",
            name: "Layout Test Definition",
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

        var layoutResult1 = DeviceLayout.Create(
            deviceDefinitionId: definitionResult.Value.Id,
            name: "Layout A",
            maxZoneCount: 4);

        layoutResult1.IsError.ShouldBeFalse();
        _writeDbContext.DeviceLayouts.Add(layoutResult1.Value);

        var layoutResult2 = DeviceLayout.Create(
            deviceDefinitionId: definitionResult.Value.Id,
            name: "Layout B",
            maxZoneCount: 2);

        layoutResult2.IsError.ShouldBeFalse();
        _writeDbContext.DeviceLayouts.Add(layoutResult2.Value);

        await _writeDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        IReadOnlyList<DeviceLayoutSnapshot> result = await _repository!.GetByDeviceDefinitionIdAsync(
            definitionResult.Value.Id,
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("Layout A");
        result[1].Name.ShouldBe("Layout B");
    }

    [Fact]
    public async Task GetByDeviceDefinitionIdAsync_ShouldReturnEmpty_WhenNoLayoutsExist()
    {
        IReadOnlyList<DeviceLayoutSnapshot> result = await _repository!.GetByDeviceDefinitionIdAsync(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_ShouldReturnPagedLayouts()
    {
        var definitionResult = DeviceDefinition.Create(
            modelId: "HS-PAGED-LAYOUT-DEF",
            name: "Paged Layout Test Definition",
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

        var layoutResult1 = DeviceLayout.Create(
            deviceDefinitionId: definitionResult.Value.Id,
            name: "Paged Layout 1",
            maxZoneCount: 3);

        layoutResult1.IsError.ShouldBeFalse();
        _writeDbContext.DeviceLayouts.Add(layoutResult1.Value);

        var layoutResult2 = DeviceLayout.Create(
            deviceDefinitionId: definitionResult.Value.Id,
            name: "Paged Layout 2",
            maxZoneCount: 6);

        layoutResult2.IsError.ShouldBeFalse();
        _writeDbContext.DeviceLayouts.Add(layoutResult2.Value);

        await _writeDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        PagedList<DeviceLayoutSnapshot> result = await _repository!.GetPagedAsync(
            page: 1,
            size: 10,
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.TotalItems.ShouldBeGreaterThanOrEqualTo(2);
        result.Items.ShouldNotBeEmpty();
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

            return new DeviceReadDbContext(options);
        }
    }
}
