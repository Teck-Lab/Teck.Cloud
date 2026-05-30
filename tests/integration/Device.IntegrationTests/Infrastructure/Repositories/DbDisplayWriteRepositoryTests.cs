// <copyright file="DbDisplayWriteRepositoryTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Displays.Abstractions;
using Device.Domain.Entities.DisplayAggregate;
using Device.Infrastructure.Persistence;
using Device.Infrastructure.Persistence.Repositories.Write;
using Device.IntegrationTests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Device.IntegrationTests.Infrastructure.Repositories;

[Collection("SharedDeviceTestcontainers")]
public sealed class DbDisplayWriteRepositoryTests : IAsyncLifetime
{
    private readonly SharedDeviceTestcontainersFixture _fixture;
    private DeviceWriteDbContext? _dbContext;
    private IDisplayWriteRepository? _repository;

    public DbDisplayWriteRepositoryTests(SharedDeviceTestcontainersFixture fixture)
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

        var tenantAccessor = new FixedTenantContextAccessor();
        _dbContext = new DeviceWriteDbContext(options, tenantAccessor);

        await _dbContext.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        await _dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        _repository = new DbDisplayWriteRepository(_dbContext, new TestHttpContextAccessor());
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
    public async Task AddAsync_ShouldPersistDisplay()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Arrange
        global::ErrorOr.ErrorOr<Display> created = Display.Create(
            shortSerial: "AE-6F-B8-87",
            locationNodeId: "shelf-a1",
            deviceDefinitionId: null);

        Display display = created.Value;

        // Act
        await _repository!.AddAsync(display, TestContext.Current.CancellationToken);
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        Display? persisted = await _dbContext.Set<Display>()
            .FirstOrDefaultAsync(d => d.Id == display.Id, TestContext.Current.CancellationToken);

        persisted.ShouldNotBeNull();
        persisted.ShortSerial.ShouldBe("AE-6F-B8-87");
        persisted.LocationNodeId.ShouldBe("shelf-a1");
    }

    [Fact]
    public async Task ExistsWithShortSerialGlobalAsync_ShouldReturnTrue_WhenSerialExists()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Arrange
        global::ErrorOr.ErrorOr<Display> created = Display.Create(
            shortSerial: "00-11-22-33",
            locationNodeId: "shelf-b2",
            deviceDefinitionId: null);

        await _repository!.AddAsync(created.Value, TestContext.Current.CancellationToken);
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        bool exists = await _repository.ExistsWithShortSerialGlobalAsync("00-11-22-33", TestContext.Current.CancellationToken);

        // Assert
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsWithShortSerialGlobalAsync_ShouldReturnFalse_WhenSerialDoesNotExist()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Act
        bool exists = await _repository!.ExistsWithShortSerialGlobalAsync("FF-FF-FF-FF", TestContext.Current.CancellationToken);

        // Assert
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsWithShortSerialGlobalAsync_ShouldBeCaseSensitive()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Arrange
        global::ErrorOr.ErrorOr<Display> created = Display.Create(
            shortSerial: "AA-BB-CC-DD",
            locationNodeId: "shelf-c3",
            deviceDefinitionId: null);

        await _repository!.AddAsync(created.Value, TestContext.Current.CancellationToken);
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        bool lowerExists = await _repository.ExistsWithShortSerialGlobalAsync("aa-bb-cc-dd", TestContext.Current.CancellationToken);
        bool upperExists = await _repository.ExistsWithShortSerialGlobalAsync("AA-BB-CC-DD", TestContext.Current.CancellationToken);

        // Assert
        upperExists.ShouldBeTrue();
    }

    [Fact]
    public async Task AddAsync_ShouldPersistMultipleDisplays()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Arrange
        global::ErrorOr.ErrorOr<Display> display1 = Display.Create(
            shortSerial: "11-22-33-44",
            locationNodeId: "shelf-a1",
            deviceDefinitionId: null);

        global::ErrorOr.ErrorOr<Display> display2 = Display.Create(
            shortSerial: "55-66-77-88",
            locationNodeId: "shelf-a1",
            deviceDefinitionId: null);

        global::ErrorOr.ErrorOr<Display> display3 = Display.Create(
            shortSerial: "99-AA-BB-CC",
            locationNodeId: "shelf-b2",
            deviceDefinitionId: null);

        // Act
        await _repository!.AddAsync(display1.Value, TestContext.Current.CancellationToken);
        await _repository.AddAsync(display2.Value, TestContext.Current.CancellationToken);
        await _repository.AddAsync(display3.Value, TestContext.Current.CancellationToken);
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        int count = await _dbContext.Set<Display>().CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBeGreaterThanOrEqualTo(3);
    }

    private sealed class TestHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}
