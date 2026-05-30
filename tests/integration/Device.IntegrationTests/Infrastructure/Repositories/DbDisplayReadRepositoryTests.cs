// <copyright file="DbDisplayReadRepositoryTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Displays.Abstractions;
using Device.Domain.Entities.DisplayAggregate;
using Device.Infrastructure.Persistence;
using Device.Infrastructure.Persistence.Repositories.Read;
using Device.IntegrationTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Device.IntegrationTests.Infrastructure.Repositories;

[Collection("SharedDeviceTestcontainers")]
public sealed class DbDisplayReadRepositoryTests : IAsyncLifetime
{
    private readonly SharedDeviceTestcontainersFixture _fixture;
    private DeviceReadDbContext? _dbContext;
    private IDisplayReadRepository? _repository;

    public DbDisplayReadRepositoryTests(SharedDeviceTestcontainersFixture fixture)
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

        _repository = new DbDisplayReadRepository(_dbContext);
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
    public async Task GetByLocationAsync_ShouldReturnDisplays_WhenLocationHasDisplays()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Arrange
        global::ErrorOr.ErrorOr<Display> display1 = Display.Create("AE-6F-B8-87", "shelf-a1", null);
        global::ErrorOr.ErrorOr<Display> display2 = Display.Create("00-11-22-33", "shelf-a1", null);
        global::ErrorOr.ErrorOr<Display> display3 = Display.Create("FF-FF-FF-FF", "shelf-b2", null);

        _dbContext!.Set<Display>().AddRange(display1.Value, display2.Value, display3.Value);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        IReadOnlyList<DisplaySnapshot> results = await _repository!.GetByLocationAsync(
            "shelf-a1",
            TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(2);
        results.ShouldContain(r => r.ShortSerial == "AE-6F-B8-87");
        results.ShouldContain(r => r.ShortSerial == "00-11-22-33");
    }

    [Fact]
    public async Task GetByLocationAsync_ShouldReturnEmptyList_WhenLocationHasNoDisplays()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Arrange
        global::ErrorOr.ErrorOr<Display> display = Display.Create("AE-6F-B8-87", "shelf-a1", null);
        _dbContext!.Set<Display>().Add(display.Value);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        IReadOnlyList<DisplaySnapshot> results = await _repository!.GetByLocationAsync(
            "nonexistent-node",
            TestContext.Current.CancellationToken);

        // Assert
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByLocationAsync_ShouldOrderByShortSerial()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Arrange
        global::ErrorOr.ErrorOr<Display> display1 = Display.Create("ZZ-ZZ-ZZ-ZZ", "shelf-a1", null);
        global::ErrorOr.ErrorOr<Display> display2 = Display.Create("AA-AA-AA-AA", "shelf-a1", null);
        global::ErrorOr.ErrorOr<Display> display3 = Display.Create("MM-MM-MM-MM", "shelf-a1", null);

        _dbContext!.Set<Display>().AddRange(display1.Value, display2.Value, display3.Value);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        IReadOnlyList<DisplaySnapshot> results = await _repository!.GetByLocationAsync(
            "shelf-a1",
            TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(3);
        results[0].ShortSerial.ShouldBe("AA-AA-AA-AA");
        results[1].ShortSerial.ShouldBe("MM-MM-MM-MM");
        results[2].ShortSerial.ShouldBe("ZZ-ZZ-ZZ-ZZ");
    }

    [Fact]
    public async Task GetByLocationAsync_ShouldIncludeOptionalDeviceDefinitionId()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Arrange
        var definitionId = Guid.NewGuid();
        global::ErrorOr.ErrorOr<Display> display = Display.Create("AE-6F-B8-87", "shelf-a1", definitionId);

        _dbContext!.Set<Display>().Add(display.Value);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        IReadOnlyList<DisplaySnapshot> results = await _repository!.GetByLocationAsync(
            "shelf-a1",
            TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(1);
        results[0].DeviceDefinitionId.ShouldBe(definitionId);
    }

    [Fact]
    public async Task GetByLocationAsync_ShouldIncludeNullLongSerial_WhenNotSet()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Arrange
        global::ErrorOr.ErrorOr<Display> display = Display.Create("AE-6F-B8-87", "shelf-a1", null);
        _dbContext!.Set<Display>().Add(display.Value);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        IReadOnlyList<DisplaySnapshot> results = await _repository!.GetByLocationAsync(
            "shelf-a1",
            TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(1);
        results[0].LongSerial.ShouldBeNull();
    }
}
