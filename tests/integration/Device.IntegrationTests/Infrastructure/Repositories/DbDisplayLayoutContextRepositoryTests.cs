// <copyright file="DbDisplayLayoutContextRepositoryTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Assignments.Abstractions;
using Device.Application.DeviceDefinitions.ReadModels;
using Device.Application.DeviceLayouts.ReadModels;
using Device.Domain.Entities.DeviceDefinitionAggregate;
using Device.Domain.Entities.DisplayAggregate;
using Device.Infrastructure.Persistence;
using Device.Infrastructure.Persistence.Repositories.Read;
using Device.IntegrationTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Device.IntegrationTests.Infrastructure.Repositories;

[Collection("SharedDeviceTestcontainers")]
public sealed class DbDisplayLayoutContextRepositoryTests : IAsyncLifetime
{
    private readonly SharedDeviceTestcontainersFixture _fixture;
    private DeviceReadDbContext? _dbContext;
    private IDeviceDefinitionReadRepository? _repository;

    public DbDisplayLayoutContextRepositoryTests(SharedDeviceTestcontainersFixture fixture)
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

        var factory = new TestDbContextFactory(_fixture.DbContainer!.GetConnectionString());
        _repository = new DbDisplayLayoutContextRepository(factory);
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
    public async Task GetLayoutContextByDisplayIdAsync_ShouldReturnContext_WhenDisplayHasLayout()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Arrange — seed definition, layout, and a display pointing at that layout
        var definitionId = Guid.NewGuid();
        var layoutId = Guid.NewGuid();

        _dbContext!.DeviceDefinitions.Add(new DeviceDefinitionReadModel
        {
            Id = definitionId,
            ModelId = "DLCR-DEF-001",
            Name = "Layout Context Definition",
            EslProvider = "Hanshow",
            SupportedColors = (int)DisplayInkColor.Black,
        });

        _dbContext.DeviceLayouts.Add(new DeviceLayoutReadModel
        {
            Id = layoutId,
            DeviceDefinitionId = definitionId,
            Name = "Three-Zone Layout",
            MaxZoneCount = 3,
        });

        var displayResult = Display.Create(
            shortSerial: "DC-AB-CD-EF",
            locationNodeId: "shelf-dlcr",
            deviceDefinitionId: definitionId,
            deviceLayoutId: layoutId);

        displayResult.IsError.ShouldBeFalse();
        _dbContext.Displays.Add(displayResult.Value);

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        DisplayLayoutContext? context = await _repository!.GetLayoutContextByDisplayIdAsync(
            displayResult.Value.Id,
            TestContext.Current.CancellationToken);

        // Assert
        context.ShouldNotBeNull();
        context!.DisplayId.ShouldBe(displayResult.Value.Id);
        context.DeviceLayoutId.ShouldBe(layoutId);
        context.MaxZoneCount.ShouldBe(3);
    }

    [Fact]
    public async Task GetLayoutContextByDisplayIdAsync_ShouldReturnNull_WhenDisplayHasNoLayout()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Arrange — seed a display without a DeviceLayoutId
        var displayResult = Display.Create(
            shortSerial: "NO-LA-YO-UT",
            locationNodeId: "shelf-no-layout",
            deviceDefinitionId: null,
            deviceLayoutId: null);

        displayResult.IsError.ShouldBeFalse();
        _dbContext!.Displays.Add(displayResult.Value);

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        DisplayLayoutContext? context = await _repository!.GetLayoutContextByDisplayIdAsync(
            displayResult.Value.Id,
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

            return new DeviceReadDbContext(options);
        }
    }

    [Fact]
    public async Task GetLayoutContextByDisplayIdAsync_ShouldReturnNull_WhenDisplayDoesNotExist()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Act
        DisplayLayoutContext? context = await _repository!.GetLayoutContextByDisplayIdAsync(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        // Assert
        context.ShouldBeNull();
    }
}
