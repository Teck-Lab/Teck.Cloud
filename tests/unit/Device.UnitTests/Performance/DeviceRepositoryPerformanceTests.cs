// <copyright file="DeviceRepositoryPerformanceTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Diagnostics;
using Device.Domain.Entities.DeviceDefinitionAggregate;
using Device.Domain.Entities.DisplayAggregate;
using Device.Infrastructure.Persistence;
using Device.Infrastructure.Persistence.Repositories.Read;
using Device.Infrastructure.Persistence.Repositories.Write;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Devices;
using SharedKernel.Infrastructure.MultiTenant;
using Shouldly;

namespace Device.UnitTests.Performance;

[Trait("Category", "Performance")]
public sealed class DeviceRepositoryPerformanceTests
{
    private static DeviceWriteDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<DeviceWriteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new DeviceWriteDbContext(options, new TestTenantAccessor());
    }

    [Fact]
    public async Task BulkAddAsync_ShouldCompleteWithinThreshold_WhenAdding1kDisplays()
    {
        // Arrange
        using DeviceWriteDbContext dbContext = CreateInMemoryContext();
        var repository = new DbDisplayWriteRepository(dbContext, new TestHttpContextAccessor());
        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        for (int i = 0; i < 1_000; i++)
        {
            global::ErrorOr.ErrorOr<Display> created = Display.Create(
                shortSerial: $"{i:X2}-{(i + 1) % 256:X2}-{(i + 2) % 256:X2}-{(i + 3) % 256:X2}",
                locationNodeId: "shelf-a1",
                deviceDefinitionId: null);

            await repository.AddAsync(created.Value, TestContext.Current.CancellationToken);
        }

        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Assert
        int count = await dbContext.Set<Display>().CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(1_000);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5_000);
    }

    [Fact]
    public async Task ConcurrentReadOperations_ShouldCompleteWithinThreshold()
    {
        // Arrange
        string dbName = Guid.NewGuid().ToString();
        var writeOptions = new DbContextOptionsBuilder<DeviceWriteDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        using DeviceWriteDbContext dbContext = new(writeOptions, new TestTenantAccessor());

        for (int i = 0; i < 1_000; i++)
        {
            global::ErrorOr.ErrorOr<Display> created = Display.Create(
                shortSerial: $"{i:X2}-00-00-00",
                locationNodeId: $"shelf-{i % 10}",
                deviceDefinitionId: null);

            dbContext.Set<Display>().Add(created.Value);
        }

        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var readOptions = new DbContextOptionsBuilder<DeviceReadDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        using var readDbContext = new DeviceReadDbContext(readOptions, new TestTenantAccessor());
        var repository = new DbDisplayReadRepository(readDbContext);
        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        Task[] tasks = new Task[10];
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = repository.GetByLocationAsync($"shelf-{i}", TestContext.Current.CancellationToken).AsTask();
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1_000);
    }

    [Fact]
    public async Task ExistsWithModelIdAsync_ShouldCompleteWithinThreshold_WhenDatasetHas10kRecords()
    {
        // Arrange
        using DeviceWriteDbContext dbContext = CreateInMemoryContext();
        var repository = new DbDeviceDefinitionWriteRepository(dbContext, new TestHttpContextAccessor());
        var stopwatch = new Stopwatch();

        for (int i = 0; i < 10_000; i++)
        {
            global::ErrorOr.ErrorOr<DeviceDefinition> created = DeviceDefinition.Create(
                modelId: $"MODEL-{i:D5}",
                name: $"Test Model {i}",
                eslProvider: EslProvider.Hanshow,
                supportedColors: DisplayInkColor.Black,
                supportsNfc: false,
                widthPx: null,
                heightPx: null,
                catalogManufacturerId: null,
                catalogSupplierId: null,
                catalogProductId: null);

            await repository.AddAsync(created.Value, TestContext.Current.CancellationToken);
        }

        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        stopwatch.Start();
        bool exists = await repository.ExistsWithModelIdAsync("MODEL-05000", TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Assert
        exists.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(500);
    }

    private sealed class TestTenantAccessor : IMultiTenantContextAccessor<TenantDetails>
    {
        public IMultiTenantContext<TenantDetails> MultiTenantContext { get; } = new MultiTenantContext<TenantDetails>(
            new TenantDetails
            {
                Id = "test-tenant",
                Identifier = "test-tenant",
                Name = "Test Tenant",
                IsActive = true,
            });

        IMultiTenantContext IMultiTenantContextAccessor.MultiTenantContext => this.MultiTenantContext;
    }

    private sealed class TestHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}
