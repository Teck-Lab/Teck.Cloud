using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.MultiTenancy;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.CheckoutWork.V1;

/// <summary>Binds an authoritative provider checkout session under the order lease.</summary>
/// <param name="TenantId">The validated tenant id.</param>
/// <param name="OrderId">The order id.</param>
/// <param name="JobId">The claimed reconciliation job id.</param>
/// <param name="LeaseToken">The claimed lease token.</param>
/// <param name="SessionId">The provider session id.</param>
/// <param name="CheckoutUrl">The provider-hosted HTTPS URL.</param>
/// <param name="Mode">The provider account mode.</param>
/// <param name="AccountId">The provider account id.</param>
public sealed record ApplyCheckoutSessionCommand(
    string TenantId,
    Guid OrderId,
    Guid JobId,
    Guid LeaseToken,
    string SessionId,
    string CheckoutUrl,
    string? Mode,
    string? AccountId) : ICommand<ErrorOr<bool>>, ITenantMessage;
