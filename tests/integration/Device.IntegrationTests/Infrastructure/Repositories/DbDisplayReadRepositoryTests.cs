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
using Teck.Cloud.IntegrationTests.Shared;

namespace Device.IntegrationTests.Infrastructure.Repositories;

[Collection("SharedTestcontainers")]
public sealed class DbDisplayReadRepositoryTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private DeviceReadDbContext? _readDbContext;
    private DeviceWriteDbContext? _writeDbContext;
    private IDisplayReadRepository? _repository;
    private string? _connectionString;

    public DbDisplayReadRepositoryTests(SharedTestcontainersFixture fixture)
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

        // Create read context for the repository
        var readOptions = new DbContextOptionsBuilder<DeviceReadDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        _readDbContext = new DeviceReadDbContext(readOptions, tenantAccessor);

        _repository = new DbDisplayReadRepository(_readDbContext);
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
    public async Task GetByLocationAsync_ShouldReturnDisplays_WhenLocationHasDisplays()
    {
        // Arrange
        global::ErrorOr.ErrorOr<Display> display1 = Display.Create("AE-6F-B8-87", "shelf-a1", null);
        global::ErrorOr.ErrorOr<Display> display2 = Display.Create("00-11-22-33", "shelf-a1", null);
        global::ErrorOr.ErrorOr<Display> display3 = Display.Create("FF-FF-FF-FF", "shelf-b2", null);

        _writeDbContext!.Set<Display>().AddRange(display1.Value, display2.Value, display3.Value);
        await _writeDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        // Arrange
        global::ErrorOr.ErrorOr<Display> display = Display.Create("AE-6F-B8-87", "shelf-a1", null);
        _writeDbContext!.Set<Display>().Add(display.Value);
        await _writeDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        // Arrange
        global::ErrorOr.ErrorOr<Display> display1 = Display.Create("ZZ-ZZ-ZZ-ZZ", "shelf-a1", null);
        global::ErrorOr.ErrorOr<Display> display2 = Display.Create("AA-AA-AA-AA", "shelf-a1", null);
        global::ErrorOr.ErrorOr<Display> display3 = Display.Create("MM-MM-MM-MM", "shelf-a1", null);

        _writeDbContext!.Set<Display>().AddRange(display1.Value, display2.Value, display3.Value);
        await _writeDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        // Arrange
        var definitionId = Guid.NewGuid();
        global::ErrorOr.ErrorOr<Display> display = Display.Create("AE-6F-B8-87", "shelf-a1", definitionId);

        _writeDbContext!.Set<Display>().Add(display.Value);
        await _writeDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        // Arrange
        global::ErrorOr.ErrorOr<Display> display = Display.Create("AE-6F-B8-87", "shelf-a1", null);
        _writeDbContext!.Set<Display>().Add(display.Value);
        await _writeDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        IReadOnlyList<DisplaySnapshot> results = await _repository!.GetByLocationAsync(
            "shelf-a1",
            TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(1);
        results[0].LongSerial.ShouldBeNull();
    }
}
