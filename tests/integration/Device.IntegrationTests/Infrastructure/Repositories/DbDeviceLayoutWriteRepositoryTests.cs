// <copyright file="DbDeviceLayoutWriteRepositoryTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceLayouts.Abstractions;
using Device.Domain.Entities.DeviceLayoutAggregate;
using Device.Infrastructure.Persistence;
using Device.Infrastructure.Persistence.Repositories.Write;
using Device.IntegrationTests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Device.IntegrationTests.Infrastructure.Repositories;

[Collection("SharedDeviceTestcontainers")]
public sealed class DbDeviceLayoutWriteRepositoryTests : IAsyncLifetime
{
    private readonly SharedDeviceTestcontainersFixture _fixture;
    private DeviceWriteDbContext? _dbContext;
    private IDeviceLayoutWriteRepository? _repository;

    public DbDeviceLayoutWriteRepositoryTests(SharedDeviceTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var options = new DbContextOptionsBuilder<DeviceWriteDbContext>()
            .UseNpgsql(_fixture.DbContainer!.GetConnectionString())
            .Options;

        _dbContext = new DeviceWriteDbContext(options);

        await _dbContext.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        await _dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        _repository = new DbDeviceLayoutWriteRepository(_dbContext, new TestHttpContextAccessor());
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
    public async Task AddAsync_ShouldPersistDeviceLayout()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Arrange
        var definitionId = Guid.NewGuid();
        global::ErrorOr.ErrorOr<DeviceLayout> created = DeviceLayout.Create(
            deviceDefinitionId: definitionId,
            name: "3-zone standard",
            maxZoneCount: 3);

        DeviceLayout layout = created.Value;

        // Act
        await _repository!.AddAsync(layout, TestContext.Current.CancellationToken);
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        DeviceLayout? persisted = await _dbContext.Set<DeviceLayout>()
            .FirstOrDefaultAsync(l => l.Id == layout.Id, TestContext.Current.CancellationToken);

        persisted.ShouldNotBeNull();
        persisted.Name.ShouldBe("3-zone standard");
        persisted.MaxZoneCount.ShouldBe(3);
        persisted.DeviceDefinitionId.ShouldBe(definitionId);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnLayout_WhenExists()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Arrange
        global::ErrorOr.ErrorOr<DeviceLayout> created = DeviceLayout.Create(
            deviceDefinitionId: Guid.NewGuid(),
            name: "Single-zone",
            maxZoneCount: 1);

        DeviceLayout layout = created.Value;
        await _repository!.AddAsync(layout, TestContext.Current.CancellationToken);
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        DeviceLayout? result = await _repository.GetByIdAsync(layout.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(layout.Id);
        result.Name.ShouldBe("Single-zone");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenDoesNotExist()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Act
        DeviceLayout? result = await _repository!.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task AddAsync_ShouldPersistMultipleLayouts()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Arrange
        var definitionId = Guid.NewGuid();
        global::ErrorOr.ErrorOr<DeviceLayout> layout1 = DeviceLayout.Create(
            deviceDefinitionId: definitionId,
            name: "Layout A",
            maxZoneCount: 2);

        global::ErrorOr.ErrorOr<DeviceLayout> layout2 = DeviceLayout.Create(
            deviceDefinitionId: definitionId,
            name: "Layout B",
            maxZoneCount: 5);

        // Act
        await _repository!.AddAsync(layout1.Value, TestContext.Current.CancellationToken);
        await _repository.AddAsync(layout2.Value, TestContext.Current.CancellationToken);
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        int count = await _dbContext.Set<DeviceLayout>().CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBeGreaterThanOrEqualTo(2);
    }

    private sealed class TestHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}
