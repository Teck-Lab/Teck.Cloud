using Billing.Domain.Entities.BillingTransactionAggregate;
using ErrorOr;
using Shouldly;

namespace Billing.UnitTests.Domain.Entities.BillingTransactionAggregate;

public sealed class BillingTransactionTests
{
    [Fact]
    public void Create_WhenArgsAreValid_ShouldReturnPendingTransactionWithMappedValues()
    {
        // Arrange
        BillingTransactionCreateArgs args = CreateArgs();

        // Act
        ErrorOr<BillingTransaction> result = BillingTransaction.Create(args);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.TenantId.ShouldBe(args.TenantId);
        result.Value.CorrelationId.ShouldBe(args.CorrelationId);
        result.Value.Amount.ShouldBe(args.Amount);
        result.Value.Currency.ShouldBe(args.Currency);
        result.Value.PaymentMethodId.ShouldBe(args.PaymentMethodId);
        result.Value.Description.ShouldBe(args.Description);
        result.Value.Status.ShouldBe(BillingTransactionStatus.Pending);
        result.Value.UpdatedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Create_WhenArgsAreNull_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => BillingTransaction.Create(null!);

        // Assert
        Should.Throw<ArgumentNullException>(act);
    }

    [Fact]
    public void Create_WhenCreated_ShouldNotAddDomainEvents()
    {
        // Arrange
        BillingTransactionCreateArgs args = CreateArgs();

        // Act
        ErrorOr<BillingTransaction> result = BillingTransaction.Create(args);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void MarkSucceeded_WhenExternalChargeIdIsValid_ShouldUpdateStatusAndChargeId()
    {
        // Arrange
        BillingTransaction transaction = BillingTransaction.Create(CreateArgs()).Value;
        DateTimeOffset updatedAtBefore = transaction.UpdatedAt;

        // Act
        transaction.MarkSucceeded("ch_123");

        // Assert
        transaction.Status.ShouldBe(BillingTransactionStatus.Succeeded);
        transaction.ExternalChargeId.ShouldBe("ch_123");
        transaction.UpdatedAt.ShouldBeGreaterThanOrEqualTo(updatedAtBefore);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkSucceeded_WhenExternalChargeIdIsMissing_ShouldThrowArgumentException(string? externalChargeId)
    {
        // Arrange
        BillingTransaction transaction = BillingTransaction.Create(CreateArgs()).Value;

        // Act
        Action act = () => transaction.MarkSucceeded(externalChargeId!);

        // Assert
        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void MarkFailed_WhenCalled_ShouldSetFailedStatusAndUpdateTimestamp()
    {
        // Arrange
        BillingTransaction transaction = BillingTransaction.Create(CreateArgs()).Value;
        DateTimeOffset updatedAtBefore = transaction.UpdatedAt;

        // Act
        transaction.MarkFailed();

        // Assert
        transaction.Status.ShouldBe(BillingTransactionStatus.Failed);
        transaction.UpdatedAt.ShouldBeGreaterThanOrEqualTo(updatedAtBefore);
    }

    [Fact]
    public void MarkFailed_WhenCalledAfterSucceeded_ShouldTransitionToFailed()
    {
        // Arrange
        BillingTransaction transaction = BillingTransaction.Create(CreateArgs()).Value;
        transaction.MarkSucceeded("ch_234");

        // Act
        transaction.MarkFailed();

        // Assert
        transaction.Status.ShouldBe(BillingTransactionStatus.Failed);
    }

    private static BillingTransactionCreateArgs CreateArgs()
    {
        return new BillingTransactionCreateArgs
        {
            TenantId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            Amount = 1299m,
            Currency = "USD",
            PaymentMethodId = "pm_123",
            Description = "Initial charge",
        };
    }
}
