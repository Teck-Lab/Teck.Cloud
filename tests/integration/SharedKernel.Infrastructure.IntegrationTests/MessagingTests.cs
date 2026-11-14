using ErrorOr;
using JasperFx;
using JasperFx.CodeGeneration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Trace;
using SharedKernel.Core.Database;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Infrastructure.Hosting;
using SharedKernel.Infrastructure.Messaging;
using SharedKernel.Persistence.Caching;
using SharedKernel.Persistence.Database;
using Shouldly;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.RabbitMQ;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Verifies real durable EF-to-local-to-RabbitMQ delivery and rollback.</summary>
[Collection(TelemetryGroup.Name)]
public sealed class MessagingTests
{
    /// <summary>Domain collection commits with writes; failures leave neither writes nor delivered events.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task TransactionsCollectEventsAndDeliverThroughRabbitMq()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var database = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await using var disposeDatabase = database.ConfigureAwait(true);
        var rabbit = new RabbitMqBuilder("rabbitmq:4-alpine").Build();
        await using var disposeRabbit = rabbit.ConfigureAwait(true);
        var redis = new RedisBuilder("redis:8-alpine").Build();
        await using var disposeRedis = redis.ConfigureAwait(true);
        await Task.WhenAll(database.StartAsync(cancellation), rabbit.StartAsync(cancellation), redis.StartAsync(cancellation)).ConfigureAwait(true);
        using var host = BuildHost(database.GetConnectionString(), rabbit.GetConnectionString(), redis.GetConnectionString());
        await using (var scope = host.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<OperationContext>().Initialize(DataScope.Global);
            _ = await scope.ServiceProvider.GetRequiredService<MessageDbContext>().Database.EnsureCreatedAsync(cancellation).ConfigureAwait(true);
        }

        await host.StartAsync(cancellation).ConfigureAwait(true);
        try
        {
            var bus = host.Services.GetRequiredService<IMessageBus>();
            var id = Guid.NewGuid();
            var cache = host.Services.GetRequiredService<IFusionCache>();
            await cache.SetAsync(CreatedLocallyHandler.CacheKey(id), "old", token: cancellation).ConfigureAwait(true);
            var success = await TelemetryEvidence.InvokeAsync(bus, id, cancellation).ConfigureAwait(true);
            success.IsError.ShouldBeFalse();
            success.Value.ShouldBe(id);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            while (!await WasDeliveredAsync(host, id, deadline.Token).ConfigureAwait(true))
            {
                await Task.Delay(50, deadline.Token).ConfigureAwait(true);
            }

            host.Services.GetRequiredService<DeliveryAttempts>().Count(id).ShouldBe(2);
            await VerifyBrokerAsync(host, rabbit, database.GetConnectionString(), cancellation).ConfigureAwait(true);
            await VerifyTenantMessagesAsync(host, cancellation).ConfigureAwait(true);
            (await cache.TryGetAsync<string>(CreatedLocallyHandler.CacheKey(id), token: cancellation).ConfigureAwait(true)).HasValue.ShouldBeFalse();
            var failedId = Guid.NewGuid();
            await cache.SetAsync(CreatedLocallyHandler.CacheKey(failedId), "preserved", token: cancellation).ConfigureAwait(true);
            _ = (await Should.ThrowAsync<InvalidOperationException>(() => bus.InvokeAsync<ErrorOr<Guid>>(new CreateRecord(failedId, Fail: true), cancellation)).ConfigureAwait(true)).ShouldBeOfType<InvalidOperationException>();
            (await cache.TryGetAsync<string>(CreatedLocallyHandler.CacheKey(failedId), token: cancellation).ConfigureAwait(true)).Value.ShouldBe("preserved");
            await using var verify = host.Services.CreateAsyncScope();
            verify.ServiceProvider.GetRequiredService<OperationContext>().Initialize(DataScope.Global);
            var context = verify.ServiceProvider.GetRequiredService<MessageDbContext>();
            (await context.Set<StoredRecord>().AnyAsync(row => row.Id == failedId, cancellation).ConfigureAwait(true)).ShouldBeFalse();
            (await context.Set<Delivery>().AnyAsync(row => row.Id == failedId, cancellation).ConfigureAwait(true)).ShouldBeFalse();
            (await context.Set<Delivery>().CountAsync(row => row.Id == id, cancellation).ConfigureAwait(true)).ShouldBe(1);
        }
        finally
        {
            await host.StopAsync(cancellation).ConfigureAwait(true);
        }
    }

    /// <summary>Ordinary application saves retain outgoing messages across a broker outage and host restart.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task ApplicationSavesRecoverOutboxAfterRestart()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var database = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await using var rabbit = new RabbitMqBuilder("rabbitmq:4-alpine").Build();
        await using var redis = new RedisBuilder("redis:8-alpine").Build();
        await Task.WhenAll(database.StartAsync(cancellation), rabbit.StartAsync(cancellation), redis.StartAsync(cancellation)).ConfigureAwait(true);
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        using (var host = BuildHost(database.GetConnectionString(), rabbit.GetConnectionString(), redis.GetConnectionString()))
        {
            await using (var setup = host.Services.CreateAsyncScope())
            {
                setup.ServiceProvider.GetRequiredService<OperationContext>().Initialize(DataScope.Global);
                _ = await setup.ServiceProvider.GetRequiredService<MessageDbContext>().Database.EnsureCreatedAsync(cancellation).ConfigureAwait(true);
            }

            await host.StartAsync(cancellation).ConfigureAwait(true);
            try
            {
                await rabbit.StopAsync(cancellation).ConfigureAwait(true);
                await SaveWhileOfflineAsync(host, database.GetConnectionString(), ids, cancellation).ConfigureAwait(true);
            }
            finally
            {
                await host.StopAsync(cancellation).ConfigureAwait(true);
            }
        }

        await rabbit.StartAsync(cancellation).ConfigureAwait(true);
        using var recovered = BuildHost(database.GetConnectionString(), rabbit.GetConnectionString(), redis.GetConnectionString());
        await recovered.StartAsync(cancellation).ConfigureAwait(true);
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            deadline.CancelAfter(TimeSpan.FromSeconds(45));
            foreach (var id in ids)
            {
                while (!await WasDeliveredAsync(recovered, id, deadline.Token).ConfigureAwait(true))
                {
                    await Task.Delay(50, deadline.Token).ConfigureAwait(true);
                }
            }

            while (await InboxDuplicateProbe.CountPendingOutgoingAsync(database.GetConnectionString(), deadline.Token).ConfigureAwait(true) != 0)
            {
                await Task.Delay(50, deadline.Token).ConfigureAwait(true);
            }
        }
        finally
        {
            await recovered.StopAsync(cancellation).ConfigureAwait(true);
        }
    }

    internal static IHost BuildHost(string database, string rabbit, string redis, Action<IHostApplicationBuilder>? configure = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddFoundationHost(exportTelemetry: false);
        _ = builder.Services.AddSingleton<ActivityCapture>();
        _ = builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddProcessor(provider => new SimpleActivityExportProcessor(provider.GetRequiredService<ActivityCapture>())));
        _ = builder.Services.AddPersistenceFoundation();
        _ = builder.Services.AddFoundationStandaloneRedis(redis);
        _ = builder.Services.AddSingleton<ITenantDirectory, TestTenantDirectory>();
        _ = builder.Services.AddSingleton<DeliveryAttempts>();
        _ = builder.Services.AddScoped<IGenericWriteRepository<StoredRecord, Guid>, GenericWriteRepository<StoredRecord, Guid, MessageDbContext>>();
        _ = builder.Services.AddDbContextWithWolverineIntegration<MessageDbContext>(options => options.UseNpgsql(database), "messages");
        _ = builder.UseWolverine(options =>
        {
            var output = Environment.GetEnvironmentVariable("TECK_CODEGEN_OUTPUT");
            if (!string.IsNullOrEmpty(output))
            {
                options.CodeGeneration.GeneratedCodeOutputPath = output;
                options.CodeGeneration.SourceCodeWritingEnabled = true;
            }
#if VERIFY_STATIC_HANDLERS
            options.CodeGeneration.TypeLoadMode = TypeLoadMode.Static;
#else
            options.CodeGeneration.TypeLoadMode = TypeLoadMode.Auto;
#endif
            _ = options.Discovery.IncludeAssembly(typeof(MessagingTests).Assembly);
            _ = options.OnException<TimeoutException>().RetryWithCooldown(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100));
            options.UseFoundationMessaging<MessageDbContext>(database, "messages");
            options.UseRequiredRabbitMq(new Uri(rabbit));
            _ = options.UseRabbitMq(new Uri(rabbit)).AutoProvision()
                .DeclareExchange("test-exchange", exchange => exchange.BindQueue("test-deliveries"));
            _ = options.ListenToRabbitQueue("test-deliveries").Sequential();
            options.RouteDomainEvent<CreatedLocally>("domain-events");
            options.RouteIntegrationEvent<PublishedExternally>("test-exchange");
            options.RouteDomainEvent<TenantRecordCreated>("tenant-events");
            options.RouteIntegrationEvent<TenantRecordPublished>("test-exchange");
            _ = options.Services.Configure<JasperFxOptions>(configuration =>
            {
                configuration.Production.ResourceAutoCreate = AutoCreate.All;
                configuration.Production.SourceCodeWritingEnabled = true;
                configuration.Production.GeneratedCodeOutputPath = output ?? Path.Combine(builder.Environment.ContentRootPath, ".artifacts", "wolverine-generated");
#if VERIFY_STATIC_HANDLERS
                configuration.Production.GeneratedCodeMode = TypeLoadMode.Static;
#else
                configuration.Production.GeneratedCodeMode = TypeLoadMode.Auto;
#endif
            });
        });
        configure?.Invoke(builder);
        return builder.Build();
    }

    private static async Task VerifyBrokerAsync(IHost host, RabbitMqContainer rabbit, string database, CancellationToken cancellation)
    {
        var queues = await rabbit.ExecAsync(["rabbitmqctl", "list_queues", "name"], cancellation).ConfigureAwait(false);
        queues.ExitCode.ShouldBe(0);
        queues.Stdout.ShouldContain("wolverine.control.", Case.Sensitive);
        await InboxDuplicateProbe.VerifyAsync(host, rabbit.GetConnectionString(), database, cancellation).ConfigureAwait(false);
        await TelemetryEvidence.VerifyAsync(host, cancellation).ConfigureAwait(false);
    }

    private static async Task SaveWhileOfflineAsync(IHost host, string database, Guid[] ids, CancellationToken cancellation)
    {
        var scope = host.Services.CreateAsyncScope();
        await using var disposescope = scope.ConfigureAwait(false);
        scope.ServiceProvider.GetRequiredService<OperationContext>().Initialize(DataScope.Global);
        var context = scope.ServiceProvider.GetRequiredService<MessageDbContext>();
        var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        unit.ShouldBeSameAs(context);
        foreach (var id in ids)
        {
            var record = new StoredRecord(id);
            record.AddDomainEvent(new CreatedLocally { RecordId = id });
            _ = context.Add(record);
            (await unit.SaveChangesAsync(cancellation).ConfigureAwait(false) > 0).ShouldBeTrue();
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        while (await InboxDuplicateProbe.CountPendingOutgoingAsync(database, deadline.Token).ConfigureAwait(false) < ids.Length)
        {
            await Task.Delay(50, deadline.Token).ConfigureAwait(false);
        }

        var verify = host.Services.CreateAsyncScope();
        await using var disposeverify = verify.ConfigureAwait(false);
        verify.ServiceProvider.GetRequiredService<OperationContext>().Initialize(DataScope.Global);
        var read = verify.ServiceProvider.GetRequiredService<MessageDbContext>();
        (await read.Set<StoredRecord>().CountAsync(cancellation).ConfigureAwait(false)).ShouldBe(ids.Length);
        (await read.Set<Delivery>().ToListAsync(cancellation).ConfigureAwait(false)).ShouldBeEmpty();
    }

    private static async Task VerifyTenantMessagesAsync(IHost host, CancellationToken cancellation)
    {
        var bus = host.Services.GetRequiredService<IMessageBus>();
        var alpha = new CreateTenantRecord(Guid.NewGuid(), "alpha");
        var beta = new CreateTenantRecord(Guid.NewGuid(), "beta");
        var results = await Task.WhenAll(bus.InvokeForTenantAsync<ErrorOr<Guid>>("alpha", alpha, cancellation), bus.InvokeForTenantAsync<ErrorOr<Guid>>("beta", beta, cancellation)).ConfigureAwait(false);
        foreach (var result in results)
        {
            result.IsError.ShouldBeFalse();
        }

        _ = (await Should.ThrowAsync<InvalidOperationException>(() => bus.InvokeForTenantAsync<ErrorOr<Guid>>("inactive", new CreateTenantRecord(Guid.NewGuid(), "inactive"), cancellation)).ConfigureAwait(false)).ShouldBeOfType<InvalidOperationException>();
        _ = (await Should.ThrowAsync<InvalidOperationException>(() => bus.InvokeAsync<ErrorOr<Guid>>(new CreateTenantRecord(Guid.NewGuid(), "alpha"), new DeliveryOptions { TenantId = "beta" }, cancellation)).ConfigureAwait(false)).ShouldBeOfType<InvalidOperationException>();
        _ = (await Should.ThrowAsync<InvalidOperationException>(() => bus.InvokeAsync<ErrorOr<Guid>>(new CreateTenantRecord(Guid.NewGuid(), "alpha"), cancellation)).ConfigureAwait(false)).ShouldBeOfType<InvalidOperationException>();
        var scope = host.Services.CreateAsyncScope();
        await using var disposeScope = scope.ConfigureAwait(false);
        scope.ServiceProvider.GetRequiredService<OperationContext>().Initialize(DataScope.ForTenant("alpha"));
        var records = await scope.ServiceProvider.GetRequiredService<MessageDbContext>().Set<TenantRecord>().ToListAsync(cancellation).ConfigureAwait(false);
        records.ShouldHaveSingleItem().Id.ShouldBe(alpha.Id);
        await VerifyTenantReceiptsAsync(host, alpha.Id, beta.Id, cancellation).ConfigureAwait(false);
    }

    private static async Task VerifyTenantReceiptsAsync(IHost host, Guid alpha, Guid beta, CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        foreach (var (tenant, id) in new[] { ("alpha", alpha), ("beta", beta) })
        {
            var scope = host.Services.CreateAsyncScope();
            await using var disposeScope = scope.ConfigureAwait(false);
            scope.ServiceProvider.GetRequiredService<OperationContext>().Initialize(DataScope.ForTenant(tenant));
            var context = scope.ServiceProvider.GetRequiredService<MessageDbContext>();
            while (!await context.Set<TenantReceipt>().AnyAsync(receipt => receipt.Id == id, deadline.Token).ConfigureAwait(false))
            {
                await Task.Delay(50, deadline.Token).ConfigureAwait(false);
            }

            var receipt = (await context.Set<TenantReceipt>().ToListAsync(deadline.Token).ConfigureAwait(false)).ShouldHaveSingleItem();
            receipt.TenantId.ShouldBe(tenant);
            receipt.Id.ShouldBe(id);
        }
    }

    private static async Task<bool> WasDeliveredAsync(IHost host, Guid id, CancellationToken cancellation)
    {
        var scope = host.Services.CreateAsyncScope();
        await using var disposeScope = scope.ConfigureAwait(false);
        scope.ServiceProvider.GetRequiredService<OperationContext>().Initialize(DataScope.Global);
        return await scope.ServiceProvider.GetRequiredService<MessageDbContext>().Set<Delivery>()
            .AnyAsync(row => row.Id == id, cancellation).ConfigureAwait(false);
    }
}
