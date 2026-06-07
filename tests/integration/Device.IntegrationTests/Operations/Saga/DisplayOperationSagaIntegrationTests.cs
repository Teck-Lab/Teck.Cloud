// <copyright file="DisplayOperationSagaIntegrationTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using Device.Application.Operations.Saga;
using Device.IntegrationTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Teck.Cloud.IntegrationTests.Shared;
using Wolverine;
using Wolverine.Persistence.Sagas;
using Wolverine.RDBMS.Sagas;
using Wolverine.Runtime;

namespace Device.IntegrationTests.Operations.Saga;

[Collection("SharedTestcontainers")]
public sealed class DisplayOperationSagaIntegrationTests
{
    private const string ProductApiRemoteAddress = "http://127.0.0.1:1";
    private const string LabelGeneratorApiRemoteAddress = "http://127.0.0.1:2";

    private readonly SharedTestcontainersFixture fixture;

    public DisplayOperationSagaIntegrationTests(SharedTestcontainersFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task PublishStartOperation_ShouldCreateSagaAndEmitStartedEvent()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using TestDeviceApiHostWithMessaging host = await StartHostAsync(cancellationToken);
        StartDisplayOperation start = CreateStartCommand(operationType: "Assign");

        // Act
        (DisplayOperationSaga startedSaga, _, _, _) = DisplayOperationSaga.Start(start, NullLogger<DisplayOperationSaga>.Instance);
        await InsertSagaAsync(host.Services, startedSaga, cancellationToken);

        // Assert
        DisplayOperationSaga saga = await LoadSagaAsync(host.Services, start.DisplayId, cancellationToken);
        saga.CurrentOperationType.ShouldBe(start.OperationType);
        saga.LocationNodeId.ShouldBe(start.LocationNodeId);
        saga.TenantId.ShouldBe(start.TenantId);
        saga.Pending.Count.ShouldBe(0);
    }

    [Fact]
    public async Task PublishMultipleStartOperations_ShouldQueueSubsequentOperations()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using TestDeviceApiHostWithMessaging host = await StartHostAsync(cancellationToken);
        Guid displayId = Guid.NewGuid();
        StartDisplayOperation first = CreateStartCommand(displayId, "Assign");
        StartDisplayOperation second = CreateStartCommand(displayId, "FlashLed", requestedAt: first.RequestedAt.AddSeconds(1));

        (DisplayOperationSaga saga, _, _, _) = DisplayOperationSaga.Start(first, NullLogger<DisplayOperationSaga>.Instance);
        await InsertSagaAsync(host.Services, saga, cancellationToken);

        // Act
        await saga.Handle(second, host.MessageBus, NullLogger<DisplayOperationSaga>.Instance);
        await UpdateSagaAsync(host.Services, saga, cancellationToken);

        // Assert
        DisplayOperationSaga persistedSaga = await LoadSagaAsync(host.Services, displayId, cancellationToken);
        persistedSaga.CurrentOperationType.ShouldBe(first.OperationType);
        persistedSaga.Pending.Count.ShouldBe(1);
        persistedSaga.Pending.Single().OperationType.ShouldBe(second.OperationType);
    }

    [Fact]
    public async Task PublishCompleted_ShouldAdvanceQueueAndStartNext()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using TestDeviceApiHostWithMessaging host = await StartHostAsync(cancellationToken);
        Guid displayId = Guid.NewGuid();
        StartDisplayOperation first = CreateStartCommand(displayId, "Assign");
        StartDisplayOperation second = CreateStartCommand(displayId, "FlashLed", requestedAt: first.RequestedAt.AddSeconds(1));
        DisplayOperationCompleted completed = new(displayId, first.OperationType, first.RequestedAt.AddSeconds(5), "{\"ok\":true}");

        (DisplayOperationSaga saga, _, _, _) = DisplayOperationSaga.Start(first, NullLogger<DisplayOperationSaga>.Instance);
        await InsertSagaAsync(host.Services, saga, cancellationToken);
        await saga.Handle(second, host.MessageBus, NullLogger<DisplayOperationSaga>.Instance);
        await UpdateSagaAsync(host.Services, saga, cancellationToken);

        // Act
        await saga.Handle(completed, host.MessageBus, NullLogger<DisplayOperationSaga>.Instance);
        await UpdateSagaAsync(host.Services, saga, cancellationToken);

        // Assert
        DisplayOperationSaga persistedSaga = await LoadSagaAsync(host.Services, displayId, cancellationToken);
        persistedSaga.CurrentOperationType.ShouldBe(second.OperationType);
        persistedSaga.Pending.Count.ShouldBe(0);
        persistedSaga.CurrentOperationStartedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task PublishFailed_ShouldRetryNextPendingOperation()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using TestDeviceApiHostWithMessaging host = await StartHostAsync(cancellationToken);
        Guid displayId = Guid.NewGuid();
        StartDisplayOperation first = CreateStartCommand(displayId, "Assign");
        StartDisplayOperation second = CreateStartCommand(displayId, "FlashLed", requestedAt: first.RequestedAt.AddSeconds(1));
        DisplayOperationFailed failed = new(displayId, first.OperationType, first.RequestedAt.AddSeconds(5), "render failed");

        (DisplayOperationSaga saga, _, _, _) = DisplayOperationSaga.Start(first, NullLogger<DisplayOperationSaga>.Instance);
        await InsertSagaAsync(host.Services, saga, cancellationToken);
        await saga.Handle(second, host.MessageBus, NullLogger<DisplayOperationSaga>.Instance);
        await UpdateSagaAsync(host.Services, saga, cancellationToken);

        // Act
        await saga.Handle(failed, host.MessageBus, NullLogger<DisplayOperationSaga>.Instance);
        await UpdateSagaAsync(host.Services, saga, cancellationToken);

        // Assert
        DisplayOperationSaga persistedSaga = await LoadSagaAsync(host.Services, displayId, cancellationToken);
        persistedSaga.CurrentOperationType.ShouldBe(second.OperationType);
        persistedSaga.Pending.Count.ShouldBe(0);
        persistedSaga.CurrentOperationStartedAt.ShouldNotBeNull();
    }

    private async Task<TestDeviceApiHostWithMessaging> StartHostAsync(CancellationToken cancellationToken)
    {
        return await TestDeviceApiHostWithMessaging.StartAsync(
            this.fixture,
            ProductApiRemoteAddress,
            LabelGeneratorApiRemoteAddress,
            cancellationToken);
    }

    private static async Task InsertSagaAsync(IServiceProvider services, DisplayOperationSaga saga, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        DatabaseSagaStorage<Guid, DisplayOperationSaga> sagaStorage = await GetSagaStorageAsync(scope, cancellationToken).ConfigureAwait(false);
        await sagaStorage.InsertAsync(saga, cancellationToken).ConfigureAwait(false);
        await sagaStorage.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpdateSagaAsync(IServiceProvider services, DisplayOperationSaga saga, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        DatabaseSagaStorage<Guid, DisplayOperationSaga> sagaStorage = await GetSagaStorageAsync(scope, cancellationToken).ConfigureAwait(false);
        await sagaStorage.UpdateAsync(saga, cancellationToken).ConfigureAwait(false);
        await sagaStorage.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<DisplayOperationSaga> LoadSagaAsync(IServiceProvider services, Guid displayId, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        DatabaseSagaStorage<Guid, DisplayOperationSaga> sagaStorage = await GetSagaStorageAsync(scope, cancellationToken).ConfigureAwait(false);
        DisplayOperationSaga? saga = await sagaStorage.LoadAsync(displayId, cancellationToken).ConfigureAwait(false);
        saga.ShouldNotBeNull($"DisplayOperationSaga {displayId} was not persisted in Wolverine saga storage.");
        return saga;
    }

    private static async Task<DatabaseSagaStorage<Guid, DisplayOperationSaga>> GetSagaStorageAsync(
        AsyncServiceScope scope,
        CancellationToken cancellationToken)
    {
        IWolverineRuntime runtime = scope.ServiceProvider.GetRequiredService<IWolverineRuntime>();
        IMessageBus messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        ISagaStorage<Guid, DisplayOperationSaga> sagaStorage = await ((ISagaSupport)runtime.Storage)
            .EnrollAndFetchSagaStorage<Guid, DisplayOperationSaga>((MessageContext)messageBus)
            .ConfigureAwait(false);

        _ = cancellationToken;
        return sagaStorage.ShouldBeOfType<DatabaseSagaStorage<Guid, DisplayOperationSaga>>();
    }

    private static StartDisplayOperation CreateStartCommand(
        Guid? displayId = null,
        string operationType = "Assign",
        DateTimeOffset? requestedAt = null)
    {
        return new StartDisplayOperation(
            displayId ?? Guid.NewGuid(),
            "zone-a",
            "test-tenant",
            operationType,
            "{}",
            requestedAt ?? DateTimeOffset.UtcNow);
    }
}
