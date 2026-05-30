// <copyright file="DbDeviceLayoutReadRepositoryTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceDefinitions.ReadModels;
using Device.Application.DeviceLayouts.Abstractions;
using Device.Application.DeviceLayouts.ReadModels;
using Device.Domain.Entities.DeviceDefinitionAggregate;
using Device.Infrastructure.Persistence;
using Device.Infrastructure.Persistence.Repositories.Read;
using Device.IntegrationTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Pagination;
using Shouldly;

namespace Device.IntegrationTests.Infrastructure.Repositories;

[Collection("SharedDeviceTestcontainers")]
public sealed class DbDeviceLayoutReadRepositoryTests : IAsyncLifetime
{
    private readonly SharedDeviceTestcontainersFixture _fixture;
    private DeviceReadDbContext? _dbContext;
    private IDeviceLayoutReadRepository? _repository;

    public DbDeviceLayoutReadRepositoryTests(SharedDeviceTestcontainersFixture fixture)
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
        _repository = new DbDeviceLayoutReadRepository(factory);
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
    public async Task GetByDeviceDefinitionIdAsync_ShouldReturnLayouts_WhenLayoutsExist()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var definitionId = Guid.NewGuid();

        _dbContext!.DeviceDefinitions.Add(new DeviceDefinitionReadModel
        {
            Id = definitionId,
            ModelId = "HS-LAYOUT-DEF-001",
            Name = "Layout Test Definition",
            EslProvider = "Hanshow",
            SupportedColors = (int)DisplayInkColor.Black,
        });

        _dbContext.DeviceLayouts.AddRange(
            new DeviceLayoutReadModel { Id = Guid.NewGuid(), DeviceDefinitionId = definitionId, Name = "Layout A", MaxZoneCount = 4 },
            new DeviceLayoutReadModel { Id = Guid.NewGuid(), DeviceDefinitionId = definitionId, Name = "Layout B", MaxZoneCount = 2 });

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        IReadOnlyList<DeviceLayoutSnapshot> result = await _repository!.GetByDeviceDefinitionIdAsync(
            definitionId,
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("Layout A");
        result[1].Name.ShouldBe("Layout B");
    }

    [Fact]
    public async Task GetByDeviceDefinitionIdAsync_ShouldReturnEmpty_WhenNoLayoutsExist()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        IReadOnlyList<DeviceLayoutSnapshot> result = await _repository!.GetByDeviceDefinitionIdAsync(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_ShouldReturnPagedLayouts()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var definitionId = Guid.NewGuid();

        _dbContext!.DeviceDefinitions.Add(new DeviceDefinitionReadModel
        {
            Id = definitionId,
            ModelId = "HS-PAGED-LAYOUT-DEF",
            Name = "Paged Layout Test Definition",
            EslProvider = "Hanshow",
            SupportedColors = (int)DisplayInkColor.Black,
        });

        _dbContext.DeviceLayouts.AddRange(
            new DeviceLayoutReadModel { Id = Guid.NewGuid(), DeviceDefinitionId = definitionId, Name = "Paged Layout 1", MaxZoneCount = 3 },
            new DeviceLayoutReadModel { Id = Guid.NewGuid(), DeviceDefinitionId = definitionId, Name = "Paged Layout 2", MaxZoneCount = 6 });

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

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
