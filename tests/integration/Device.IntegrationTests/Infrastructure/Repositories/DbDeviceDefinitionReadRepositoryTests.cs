// <copyright file="DbDeviceDefinitionReadRepositoryTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceDefinitions.Abstractions;
using Device.Domain.Entities.DeviceDefinitionAggregate;
using Device.Infrastructure.Persistence;
using Device.Infrastructure.Persistence.Repositories.Read;
using Device.IntegrationTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Devices;
using SharedKernel.Core.Pagination;
using Shouldly;
using Teck.Cloud.IntegrationTests.Shared;

namespace Device.IntegrationTests.Infrastructure.Repositories;

[Collection("SharedTestcontainers")]
public sealed class DbDeviceDefinitionReadRepositoryTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private DeviceReadDbContext? _readDbContext;
    private DeviceWriteDbContext? _writeDbContext;
    private IDeviceDefinitionReadRepository? _repository;
    private string? _connectionString;

    public DbDeviceDefinitionReadRepositoryTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create database using write context migrations (has all tables including device_definitions)
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
        _repository = new DbDeviceDefinitionReadRepository(factory);
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
    public async Task GetByModelIdAsync_ShouldReturnSnapshot_WhenDefinitionExists()
    {
        var definitionResult = DeviceDefinition.Create(
            modelId: "HS-MODEL-001",
            name: "Hanshow Model A",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black | DisplayInkColor.Red,
            supportsNfc: true,
            widthPx: 296,
            heightPx: 128,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        definitionResult.IsError.ShouldBeFalse();
        _writeDbContext!.DeviceDefinitions.Add(definitionResult.Value);
        await _writeDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DeviceDefinitionSnapshot? result = await _repository!.GetByModelIdAsync(
            "HS-MODEL-001",
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.Id.ShouldBe(definitionResult.Value.Id);
        result.ModelId.ShouldBe("HS-MODEL-001");
        result.Name.ShouldBe("Hanshow Model A");
        result.SupportsNfc.ShouldBeTrue();
        result.WidthPx.ShouldBe(296);
        result.HeightPx.ShouldBe(128);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnSnapshot_WhenDefinitionExists()
    {
        var definitionResult = DeviceDefinition.Create(
            modelId: "HS-MODEL-002",
            name: "Hanshow Model B",
            eslProvider: EslProvider.SoluM,
            supportedColors: DisplayInkColor.Black,
            supportsNfc: true,
            widthPx: 128,
            heightPx: 64,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        definitionResult.IsError.ShouldBeFalse();
        _writeDbContext!.DeviceDefinitions.Add(definitionResult.Value);
        await _writeDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DeviceDefinitionSnapshot? result = await _repository!.GetByIdAsync(
            definitionResult.Value.Id,
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.Id.ShouldBe(definitionResult.Value.Id);
        result.ModelId.ShouldBe("HS-MODEL-002");
        result.SupportsNfc.ShouldBeTrue();
        result.WidthPx.ShouldBe(128);
        result.HeightPx.ShouldBe(64);
    }

    [Fact]
    public async Task GetPagedAsync_ShouldReturnPagedItems_SortedByModelIdByDefault()
    {
        var definitionResult1 = DeviceDefinition.Create(
            modelId: "Z-MODEL",
            name: "Z Model",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        definitionResult1.IsError.ShouldBeFalse();
        _writeDbContext!.DeviceDefinitions.Add(definitionResult1.Value);

        var definitionResult2 = DeviceDefinition.Create(
            modelId: "A-MODEL",
            name: "A Model",
            eslProvider: EslProvider.SoluM,
            supportedColors: DisplayInkColor.Black,
            supportsNfc: true,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        definitionResult2.IsError.ShouldBeFalse();
        _writeDbContext.DeviceDefinitions.Add(definitionResult2.Value);

        await _writeDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        PagedList<DeviceDefinitionSnapshot> result = await _repository!.GetPagedAsync(
            page: 1,
            size: 10,
            sortBy: null,
            sortDescending: false,
            TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBe(2);
        result.Items[0].ModelId.ShouldBe("A-MODEL");
        result.Items[1].ModelId.ShouldBe("Z-MODEL");
    }

    [Fact]
    public async Task GetPagedAsync_ShouldReturnPagedItems_SortedByName()
    {
        var definitionResult1 = DeviceDefinition.Create(
            modelId: "Z-MODEL",
            name: "Z Model",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        definitionResult1.IsError.ShouldBeFalse();
        _writeDbContext!.DeviceDefinitions.Add(definitionResult1.Value);

        var definitionResult2 = DeviceDefinition.Create(
            modelId: "A-MODEL",
            name: "A Model",
            eslProvider: EslProvider.SoluM,
            supportedColors: DisplayInkColor.Black,
            supportsNfc: true,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        definitionResult2.IsError.ShouldBeFalse();
        _writeDbContext.DeviceDefinitions.Add(definitionResult2.Value);

        await _writeDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        PagedList<DeviceDefinitionSnapshot> result = await _repository!.GetPagedAsync(
            page: 1,
            size: 10,
            sortBy: "name",
            sortDescending: false,
            TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBe(2);
        result.Items[0].Name.ShouldBe("A Model");
        result.Items[1].Name.ShouldBe("Z Model");
    }

    private sealed class TestDbContextFactory(string connectionString) : IDbContextFactory<DeviceReadDbContext>
    {
        public DeviceReadDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<DeviceReadDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            return new DeviceReadDbContext(options);
        }
    }
}
