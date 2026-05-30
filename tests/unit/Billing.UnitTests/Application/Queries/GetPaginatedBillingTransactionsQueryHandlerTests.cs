using Billing.Application.Billing.Features.GetPaginatedBillingTransactions.V1;
using Billing.Application.Billing.ReadModels;
using Billing.Application.Billing.Repositories;
using NSubstitute;
using SharedKernel.Core.Pagination;
using Shouldly;

namespace Billing.UnitTests.Application.Queries;

public sealed class GetPaginatedBillingTransactionsQueryHandlerTests
{
    private readonly IBillingTransactionReadRepository billingTransactionReadRepository;
    private readonly GetPaginatedBillingTransactionsQueryHandler handler;

    public GetPaginatedBillingTransactionsQueryHandlerTests()
    {
        this.billingTransactionReadRepository = Substitute.For<IBillingTransactionReadRepository>();
        this.handler = new GetPaginatedBillingTransactionsQueryHandler(this.billingTransactionReadRepository);
    }

    [Fact]
    public async Task Handle_ShouldReturnMappedPagedTransactions_WhenTransactionsExist()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        GetPaginatedBillingTransactionsQuery query = new(2, 5, tenantId, "Succeeded");

        DateTimeOffset createdAt = DateTimeOffset.UtcNow.AddDays(-1);
        DateTimeOffset updatedAt = DateTimeOffset.UtcNow;

        BillingTransactionReadModel readModel = new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CorrelationId = Guid.NewGuid(),
            Amount = 4999m,
            Currency = "USD",
            PaymentMethodId = "pm_001",
            ExternalChargeId = "ch_001",
            StatusName = "Succeeded",
            Description = "Plan upgrade charge",
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };

        PagedList<BillingTransactionReadModel> pagedTransactions = new([readModel], totalItems: 11, page: 2, size: 5);

        this.billingTransactionReadRepository
            .GetPagedTransactionsAsync(query.Page, query.Size, query.TenantId, query.Status, Arg.Any<CancellationToken>())
            .Returns(pagedTransactions);

        // Act
        var result = await this.handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.TotalItems.ShouldBe(11);
        result.Value.Page.ShouldBe(2);
        result.Value.Size.ShouldBe(5);
        result.Value.Items.Count.ShouldBe(1);

        GetPaginatedBillingTransactionsResponse mapped = result.Value.Items[0];
        mapped.Id.ShouldBe(readModel.Id);
        mapped.TenantId.ShouldBe(readModel.TenantId);
        mapped.CorrelationId.ShouldBe(readModel.CorrelationId);
        mapped.Amount.ShouldBe(readModel.Amount);
        mapped.Currency.ShouldBe(readModel.Currency);
        mapped.PaymentMethodId.ShouldBe(readModel.PaymentMethodId);
        mapped.ExternalChargeId.ShouldBe(readModel.ExternalChargeId);
        mapped.StatusName.ShouldBe(readModel.StatusName);
        mapped.Description.ShouldBe(readModel.Description);
        mapped.CreatedAt.ShouldBe(readModel.CreatedAt);
        mapped.UpdatedAt.ShouldBe(readModel.UpdatedAt);

        await this.billingTransactionReadRepository.Received(1)
            .GetPagedTransactionsAsync(query.Page, query.Size, query.TenantId, query.Status, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyPagedTransactions_WhenNoTransactionsExist()
    {
        // Arrange
        GetPaginatedBillingTransactionsQuery query = new(1, 10, null, null);

        this.billingTransactionReadRepository
            .GetPagedTransactionsAsync(query.Page, query.Size, query.TenantId, query.Status, Arg.Any<CancellationToken>())
            .Returns(new PagedList<BillingTransactionReadModel>([], 0, 1, 10));

        // Act
        var result = await this.handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.TotalItems.ShouldBe(0);
        result.Value.Page.ShouldBe(1);
        result.Value.Size.ShouldBe(10);
        result.Value.Items.ShouldBeEmpty();
    }
}
