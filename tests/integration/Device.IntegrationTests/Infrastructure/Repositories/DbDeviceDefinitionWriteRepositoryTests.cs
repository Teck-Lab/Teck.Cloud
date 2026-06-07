// <copyright file="DbDeviceDefinitionWriteRepositoryTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceDefinitions.Abstractions;
using Device.Domain.Entities.DeviceDefinitionAggregate;
using Device.Infrastructure.Persistence;
using Device.Infrastructure.Persistence.Repositories.Write;
using Device.IntegrationTests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Devices;
using Shouldly;
using Teck.Cloud.IntegrationTests.Shared;

namespace Device.IntegrationTests.Infrastructure.Repositories;

[Collection("SharedTestcontainers")]
public sealed class DbDeviceDefinitionWriteRepositoryTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private DeviceWriteDbContext? _dbContext;
    private IDeviceDefinitionWriteRepository? _repository;
    private string? _connectionString;

    public DbDeviceDefinitionWriteRepositoryTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _connectionString = await _fixture.CreateSharedTestDatabaseAsync(
            typeof(DeviceWriteDbContext),
            "Teck.Cloud.Migrations.PostgreSQL",
            TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<DeviceWriteDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        _dbContext = new DeviceWriteDbContext(options);

        _repository = new DbDeviceDefinitionWriteRepository(_dbContext, new TestHttpContextAccessor());
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_connectionString is not null)
        {
            await _fixture.TruncateAllTablesAsync(_connectionString, TestContext.Current.CancellationToken);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task AddAsync_ShouldPersistDeviceDefinition()
    {
        // Arrange
        global::ErrorOr.ErrorOr<DeviceDefinition> created = DeviceDefinition.Create(
            modelId: "HS-WRITE-001",
            name: "Write Test Model",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black | DisplayInkColor.Red,
            supportsNfc: true,
            widthPx: 250,
            heightPx: 122,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        DeviceDefinition definition = created.Value;

        // Act
        await _repository!.AddAsync(definition, TestContext.Current.CancellationToken);
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        DeviceDefinition? persisted = await _dbContext.Set<DeviceDefinition>()
            .FirstOrDefaultAsync(d => d.Id == definition.Id, TestContext.Current.CancellationToken);

        persisted.ShouldNotBeNull();
        persisted.ModelId.ShouldBe("HS-WRITE-001");
        persisted.Name.ShouldBe("Write Test Model");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnDefinition_WhenExists()
    {
        // Arrange
        global::ErrorOr.ErrorOr<DeviceDefinition> created = DeviceDefinition.Create(
            modelId: "HS-WRITE-002",
            name: "Get By Id Model",
            eslProvider: EslProvider.SoluM,
            supportedColors: DisplayInkColor.Black,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        DeviceDefinition definition = created.Value;
        await _repository!.AddAsync(definition, TestContext.Current.CancellationToken);
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        DeviceDefinition? result = await _repository.GetByIdAsync(definition.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(definition.Id);
        result.ModelId.ShouldBe("HS-WRITE-002");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenDoesNotExist()
    {
        // Act
        DeviceDefinition? result = await _repository!.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExistsWithModelIdAsync_ShouldReturnTrue_WhenModelIdExists()
    {
        // Arrange
        global::ErrorOr.ErrorOr<DeviceDefinition> created = DeviceDefinition.Create(
            modelId: "HS-WRITE-003",
            name: "Exists Test Model",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        await _repository!.AddAsync(created.Value, TestContext.Current.CancellationToken);
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        bool exists = await _repository.ExistsWithModelIdAsync("HS-WRITE-003", TestContext.Current.CancellationToken);

        // Assert
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsWithModelIdAsync_ShouldReturnFalse_WhenModelIdDoesNotExist()
    {
        // Act
        bool exists = await _repository!.ExistsWithModelIdAsync("DOES-NOT-EXIST", TestContext.Current.CancellationToken);

        // Assert
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task AddAsync_ShouldPersistMultipleDefinitions()
    {
        // Arrange
        global::ErrorOr.ErrorOr<DeviceDefinition> def1 = DeviceDefinition.Create(
            modelId: "HS-MULTI-001",
            name: "Multi 1",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        global::ErrorOr.ErrorOr<DeviceDefinition> def2 = DeviceDefinition.Create(
            modelId: "HS-MULTI-002",
            name: "Multi 2",
            eslProvider: EslProvider.SoluM,
            supportedColors: DisplayInkColor.Black | DisplayInkColor.White,
            supportsNfc: true,
            widthPx: 300,
            heightPx: 200,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        // Act
        await _repository!.AddAsync(def1.Value, TestContext.Current.CancellationToken);
        await _repository.AddAsync(def2.Value, TestContext.Current.CancellationToken);
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        int count = await _dbContext.Set<DeviceDefinition>().CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBeGreaterThanOrEqualTo(2);
    }

    private sealed class TestHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}
