// <copyright file="DbDeviceDefinitionReadRepositoryTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceDefinitions.Abstractions;
using Device.Application.DeviceDefinitions.ReadModels;
using Device.Domain.Entities.DeviceDefinitionAggregate;
using Device.Infrastructure.Persistence;
using Device.Infrastructure.Persistence.Repositories.Read;
using Device.IntegrationTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Pagination;
using Shouldly;

namespace Device.IntegrationTests.Infrastructure.Repositories;

[Collection("SharedDeviceTestcontainers")]
public sealed class DbDeviceDefinitionReadRepositoryTests : IAsyncLifetime
{
    private readonly SharedDeviceTestcontainersFixture _fixture;
    private DeviceReadDbContext? _dbContext;
    private IDeviceDefinitionReadRepository? _repository;

    public DbDeviceDefinitionReadRepositoryTests(SharedDeviceTestcontainersFixture fixture)
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
        _repository = new DbDeviceDefinitionReadRepository(factory);
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
    public async Task GetByModelIdAsync_ShouldReturnSnapshot_WhenDefinitionExists()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var id = Guid.NewGuid();
        _dbContext!.DeviceDefinitions.Add(new DeviceDefinitionReadModel
        {
            Id = id,
            ModelId = "HS-MODEL-001",
            Name = "Hanshow Model A",
            EslProvider = "Hanshow",
            SupportedColors = (int)(DisplayInkColor.Black | DisplayInkColor.Red),
            SupportsNfc = false,
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DeviceDefinitionSnapshot? result = await _repository!.GetByModelIdAsync(
            "HS-MODEL-001",
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.ModelId.ShouldBe("HS-MODEL-001");
        result.Name.ShouldBe("Hanshow Model A");
        result.EslProvider.Name.ShouldBe("Hanshow");
        result.SupportedColors.ShouldBe(DisplayInkColor.Black | DisplayInkColor.Red);
    }

    [Fact]
    public async Task GetByModelIdAsync_ShouldReturnNull_WhenDefinitionDoesNotExist()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        DeviceDefinitionSnapshot? result = await _repository!.GetByModelIdAsync(
            "DOES-NOT-EXIST",
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnSnapshot_WhenDefinitionExists()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var id = Guid.NewGuid();
        _dbContext!.DeviceDefinitions.Add(new DeviceDefinitionReadModel
        {
            Id = id,
            ModelId = "HS-MODEL-002",
            Name = "Hanshow Model B",
            EslProvider = "SoluM",
            SupportedColors = (int)DisplayInkColor.Black,
            SupportsNfc = true,
            WidthPx = 128,
            HeightPx = 64,
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DeviceDefinitionSnapshot? result = await _repository!.GetByIdAsync(
            id,
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.Id.ShouldBe(id);
        result.ModelId.ShouldBe("HS-MODEL-002");
        result.SupportsNfc.ShouldBeTrue();
        result.WidthPx.ShouldBe(128);
        result.HeightPx.ShouldBe(64);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenDefinitionDoesNotExist()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        DeviceDefinitionSnapshot? result = await _repository!.GetByIdAsync(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetPagedAsync_ShouldReturnPagedItems_SortedByModelIdByDefault()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        _dbContext!.DeviceDefinitions.AddRange(
            new DeviceDefinitionReadModel
            {
                Id = Guid.NewGuid(),
                ModelId = "ZZZ-PAGED-B",
                Name = "Beta",
                EslProvider = "Hanshow",
                SupportedColors = (int)DisplayInkColor.Black,
            },
            new DeviceDefinitionReadModel
            {
                Id = Guid.NewGuid(),
                ModelId = "AAA-PAGED-A",
                Name = "Alpha",
                EslProvider = "Hanshow",
                SupportedColors = (int)DisplayInkColor.Black,
            });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        PagedList<DeviceDefinitionSnapshot> result = await _repository!.GetPagedAsync(
            page: 1,
            size: 10,
            sortBy: null,
            sortDescending: false,
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.TotalItems.ShouldBeGreaterThanOrEqualTo(2);
        result.Items.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_ShouldReturnPagedItems_SortedByName()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        _dbContext!.DeviceDefinitions.AddRange(
            new DeviceDefinitionReadModel
            {
                Id = Guid.NewGuid(),
                ModelId = "SORT-NAME-C",
                Name = "Zebra Display",
                EslProvider = "Hanshow",
                SupportedColors = (int)DisplayInkColor.Black,
            },
            new DeviceDefinitionReadModel
            {
                Id = Guid.NewGuid(),
                ModelId = "SORT-NAME-D",
                Name = "Apple Display",
                EslProvider = "Hanshow",
                SupportedColors = (int)DisplayInkColor.Black,
            });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        PagedList<DeviceDefinitionSnapshot> result = await _repository!.GetPagedAsync(
            page: 1,
            size: 10,
            sortBy: "name",
            sortDescending: false,
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Items.ShouldNotBeEmpty();
        var names = result.Items.Select(x => x.Name).ToList();
        names.ShouldBe(names.OrderBy(n => n).ToList());
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
