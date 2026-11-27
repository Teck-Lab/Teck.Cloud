using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.CheckoutWork.V1;

/// <summary>Persists verified prices and starts a leased provider checkout attempt.</summary>
/// <param name="TenantId">The validated tenant id.</param>
/// <param name="OrderId">The order id.</param>
/// <param name="JobId">The claimed reconciliation job id.</param>
/// <param name="LeaseToken">The claimed lease token.</param>
/// <param name="Lines">The verified provider price lines.</param>
public sealed record BeginCheckoutAttemptCommand(
    string TenantId,
    Guid OrderId,
    Guid JobId,
    Guid LeaseToken,
    IReadOnlyList<BillingVerifiedPriceLine> Lines) : ICommand<ErrorOr<bool>>, ITenantMessage;
