using Billing.Application.Billing.Sagas;
using Billing.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SharedKernel.Events;
using Shouldly;
using Wolverine;

namespace Billing.UnitTests.Application.Sagas;

public sealed class PlanUpgradeSagaTests
{
    [Fact]
    public void Start_ShouldMapAllFieldsFromEvent()
    {
        // Arrange
        TenantPlanUpgradeRequestedIntegrationEvent evt = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Basic",
            "Pro",
            2599m,
            "pm_123",
            "USD");

        // Act
        PlanUpgradeSaga saga = PlanUpgradeSaga.Start(evt);

        // Assert
        saga.Id.ShouldBe(evt.CorrelationId);
        saga.TenantId.ShouldBe(evt.TenantId);
        saga.CurrentPlan.ShouldBe(evt.CurrentPlan);
        saga.TargetPlan.ShouldBe(evt.TargetPlan);
        saga.ProratedAmount.ShouldBe(evt.ProratedAmount);
        saga.PaymentMethodId.ShouldBe(evt.PaymentMethodId);
        saga.Currency.ShouldBe(evt.Currency);
    }

    [Fact]
    public async Task Handle_ShouldPublishSucceededEventAndComplete_WhenChargeSucceeds()
    {
        // Arrange
        TenantPlanUpgradeRequestedIntegrationEvent evt = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Basic",
            "Business",
            1499m,
            "pm_success",
            "EUR");

        PlanUpgradeSaga saga = PlanUpgradeSaga.Start(evt);
        IPaymentGateway gateway = Substitute.For<IPaymentGateway>();
        IMessageBus bus = Substitute.For<IMessageBus>();
        ILogger<PlanUpgradeSaga> logger = Substitute.For<ILogger<PlanUpgradeSaga>>();

        const string chargeId = "ch_123";
        gateway.ChargeAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(chargeId);

        string expectedDescription = $"Plan upgrade {evt.CurrentPlan} → {evt.TargetPlan} for tenant {evt.TenantId}";

        // Act
        await saga.Handle(evt, gateway, bus, logger, TestContext.Current.CancellationToken);

        // Assert
        await gateway.Received(1).ChargeAsync(evt.PaymentMethodId, evt.ProratedAmount, evt.Currency, expectedDescription, Arg.Any<CancellationToken>());
        await bus.Received(1).PublishAsync(Arg.Is<PlanUpgradePaymentSucceededIntegrationEvent>(published =>
            published.CorrelationId == evt.CorrelationId &&
            published.TenantId == evt.TenantId &&
            published.TargetPlan == evt.TargetPlan &&
            published.ChargeId == chargeId));
        await bus.DidNotReceive().PublishAsync(Arg.Any<PlanUpgradePaymentFailedIntegrationEvent>());
        saga.IsCompleted().ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_ShouldPublishFailedEventAndComplete_WhenChargeThrows()
    {
        // Arrange
        TenantPlanUpgradeRequestedIntegrationEvent evt = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Starter",
            "Enterprise",
            9900m,
            "pm_failure",
            "USD");

        PlanUpgradeSaga saga = PlanUpgradeSaga.Start(evt);
        IPaymentGateway gateway = Substitute.For<IPaymentGateway>();
        IMessageBus bus = Substitute.For<IMessageBus>();
        ILogger<PlanUpgradeSaga> logger = Substitute.For<ILogger<PlanUpgradeSaga>>();

        InvalidOperationException paymentException = new("card declined");
        gateway.ChargeAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw paymentException);

        // Act
        await saga.Handle(evt, gateway, bus, logger, TestContext.Current.CancellationToken);

        // Assert
        await bus.Received(1).PublishAsync(Arg.Is<PlanUpgradePaymentFailedIntegrationEvent>(published =>
            published.CorrelationId == evt.CorrelationId &&
            published.TenantId == evt.TenantId &&
            published.Reason == paymentException.Message));
        await bus.DidNotReceive().PublishAsync(Arg.Any<PlanUpgradePaymentSucceededIntegrationEvent>());
        saga.IsCompleted().ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_ShouldThrowArgumentNullException_WhenEventIsNull()
    {
        // Arrange
        PlanUpgradeSaga saga = new();
        IPaymentGateway gateway = Substitute.For<IPaymentGateway>();
        IMessageBus bus = Substitute.For<IMessageBus>();
        ILogger<PlanUpgradeSaga> logger = Substitute.For<ILogger<PlanUpgradeSaga>>();

        // Act
        Func<Task> act = () => saga.Handle(null!, gateway, bus, logger, TestContext.Current.CancellationToken);

        // Assert
        await Should.ThrowAsync<ArgumentNullException>(act);
    }

    [Fact]
    public async Task Handle_ShouldThrowArgumentNullException_WhenGatewayIsNull()
    {
        // Arrange
        TenantPlanUpgradeRequestedIntegrationEvent evt = new(Guid.NewGuid(), Guid.NewGuid(), "Basic", "Pro", 100m, "pm", "USD");
        PlanUpgradeSaga saga = PlanUpgradeSaga.Start(evt);
        IMessageBus bus = Substitute.For<IMessageBus>();
        ILogger<PlanUpgradeSaga> logger = Substitute.For<ILogger<PlanUpgradeSaga>>();

        // Act
        Func<Task> act = () => saga.Handle(evt, null!, bus, logger, TestContext.Current.CancellationToken);

        // Assert
        await Should.ThrowAsync<ArgumentNullException>(act);
    }

    [Fact]
    public async Task Handle_ShouldThrowArgumentNullException_WhenBusIsNull()
    {
        // Arrange
        TenantPlanUpgradeRequestedIntegrationEvent evt = new(Guid.NewGuid(), Guid.NewGuid(), "Basic", "Pro", 100m, "pm", "USD");
        PlanUpgradeSaga saga = PlanUpgradeSaga.Start(evt);
        IPaymentGateway gateway = Substitute.For<IPaymentGateway>();
        ILogger<PlanUpgradeSaga> logger = Substitute.For<ILogger<PlanUpgradeSaga>>();

        // Act
        Func<Task> act = () => saga.Handle(evt, gateway, null!, logger, TestContext.Current.CancellationToken);

        // Assert
        await Should.ThrowAsync<ArgumentNullException>(act);
    }
}
