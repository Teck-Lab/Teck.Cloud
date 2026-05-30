using Billing.Application.Billing.Sagas;
using Billing.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SharedKernel.Events;
using Shouldly;
using Wolverine;

namespace Billing.UnitTests.Application.Sagas;

public sealed class LicenseLimitIncreaseSagaTests
{
    [Fact]
    public void Start_ShouldMapAllFieldsFromEvent()
    {
        // Arrange
        LicenseLimitIncreaseRequestedIntegrationEvent evt = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "MaxUsers",
            25,
            50,
            3400m,
            "pm_abc",
            "USD");

        // Act
        LicenseLimitIncreaseSaga saga = LicenseLimitIncreaseSaga.Start(evt);

        // Assert
        saga.Id.ShouldBe(evt.CorrelationId);
        saga.TenantId.ShouldBe(evt.TenantId);
        saga.LicenseId.ShouldBe(evt.LicenseId);
        saga.FeatureKey.ShouldBe(evt.FeatureKey);
        saga.NewLimit.ShouldBe(evt.NewLimit);
        saga.ProratedAmount.ShouldBe(evt.ProratedAmount);
        saga.PaymentMethodId.ShouldBe(evt.PaymentMethodId);
        saga.Currency.ShouldBe(evt.Currency);
    }

    [Fact]
    public async Task Handle_ShouldPublishSucceededEventAndComplete_WhenChargeSucceeds()
    {
        // Arrange
        LicenseLimitIncreaseRequestedIntegrationEvent evt = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "MaxDevices",
            10,
            30,
            2750m,
            "pm_success",
            "EUR");

        LicenseLimitIncreaseSaga saga = LicenseLimitIncreaseSaga.Start(evt);
        IPaymentGateway gateway = Substitute.For<IPaymentGateway>();
        IMessageBus bus = Substitute.For<IMessageBus>();
        ILogger<LicenseLimitIncreaseSaga> logger = Substitute.For<ILogger<LicenseLimitIncreaseSaga>>();

        const string chargeId = "ch_li_001";
        gateway.ChargeAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(chargeId);

        string expectedDescription = $"License limit increase {evt.FeatureKey} → {evt.NewLimit} for tenant {evt.TenantId} license {evt.LicenseId}";

        // Act
        await saga.Handle(evt, gateway, bus, logger, TestContext.Current.CancellationToken);

        // Assert
        await gateway.Received(1).ChargeAsync(evt.PaymentMethodId, evt.ProratedAmount, evt.Currency, expectedDescription, Arg.Any<CancellationToken>());
        await bus.Received(1).PublishAsync(Arg.Is<LicenseLimitIncreasePaymentSucceededIntegrationEvent>(published =>
            published.CorrelationId == evt.CorrelationId &&
            published.TenantId == evt.TenantId &&
            published.LicenseId == evt.LicenseId &&
            published.FeatureKey == evt.FeatureKey &&
            published.NewLimit == evt.NewLimit &&
            published.ChargeId == chargeId));
        await bus.DidNotReceive().PublishAsync(Arg.Any<LicenseLimitIncreasePaymentFailedIntegrationEvent>());
        saga.IsCompleted().ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_ShouldPublishFailedEventAndComplete_WhenChargeThrows()
    {
        // Arrange
        LicenseLimitIncreaseRequestedIntegrationEvent evt = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "MaxApiCalls",
            1000,
            5000,
            12000m,
            "pm_failure",
            "USD");

        LicenseLimitIncreaseSaga saga = LicenseLimitIncreaseSaga.Start(evt);
        IPaymentGateway gateway = Substitute.For<IPaymentGateway>();
        IMessageBus bus = Substitute.For<IMessageBus>();
        ILogger<LicenseLimitIncreaseSaga> logger = Substitute.For<ILogger<LicenseLimitIncreaseSaga>>();

        TimeoutException paymentException = new("gateway timeout");
        gateway.ChargeAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw paymentException);

        // Act
        await saga.Handle(evt, gateway, bus, logger, TestContext.Current.CancellationToken);

        // Assert
        await bus.Received(1).PublishAsync(Arg.Is<LicenseLimitIncreasePaymentFailedIntegrationEvent>(published =>
            published.CorrelationId == evt.CorrelationId &&
            published.TenantId == evt.TenantId &&
            published.LicenseId == evt.LicenseId &&
            published.Reason == paymentException.Message));
        await bus.DidNotReceive().PublishAsync(Arg.Any<LicenseLimitIncreasePaymentSucceededIntegrationEvent>());
        saga.IsCompleted().ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_ShouldThrowArgumentNullException_WhenEventIsNull()
    {
        // Arrange
        LicenseLimitIncreaseSaga saga = new();
        IPaymentGateway gateway = Substitute.For<IPaymentGateway>();
        IMessageBus bus = Substitute.For<IMessageBus>();
        ILogger<LicenseLimitIncreaseSaga> logger = Substitute.For<ILogger<LicenseLimitIncreaseSaga>>();

        // Act
        Func<Task> act = () => saga.Handle(null!, gateway, bus, logger, TestContext.Current.CancellationToken);

        // Assert
        await Should.ThrowAsync<ArgumentNullException>(act);
    }

    [Fact]
    public async Task Handle_ShouldThrowArgumentNullException_WhenGatewayIsNull()
    {
        // Arrange
        LicenseLimitIncreaseRequestedIntegrationEvent evt = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "MaxUsers", 10, 20, 100m, "pm", "USD");
        LicenseLimitIncreaseSaga saga = LicenseLimitIncreaseSaga.Start(evt);
        IMessageBus bus = Substitute.For<IMessageBus>();
        ILogger<LicenseLimitIncreaseSaga> logger = Substitute.For<ILogger<LicenseLimitIncreaseSaga>>();

        // Act
        Func<Task> act = () => saga.Handle(evt, null!, bus, logger, TestContext.Current.CancellationToken);

        // Assert
        await Should.ThrowAsync<ArgumentNullException>(act);
    }

    [Fact]
    public async Task Handle_ShouldThrowArgumentNullException_WhenBusIsNull()
    {
        // Arrange
        LicenseLimitIncreaseRequestedIntegrationEvent evt = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "MaxUsers", 10, 20, 100m, "pm", "USD");
        LicenseLimitIncreaseSaga saga = LicenseLimitIncreaseSaga.Start(evt);
        IPaymentGateway gateway = Substitute.For<IPaymentGateway>();
        ILogger<LicenseLimitIncreaseSaga> logger = Substitute.For<ILogger<LicenseLimitIncreaseSaga>>();

        // Act
        Func<Task> act = () => saga.Handle(evt, gateway, null!, logger, TestContext.Current.CancellationToken);

        // Assert
        await Should.ThrowAsync<ArgumentNullException>(act);
    }
}
