using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

namespace Teck.Cloud.Commerce.Orders.Application.Baskets.Features.PutBasket.V1;

/// <summary>Creates, replaces or clears the single basket for an authenticated subject.</summary>
/// <param name="TenantId">The validated tenant id.</param>
/// <param name="SubjectId">The authenticated subject.</param>
/// <param name="OfferId">The selected offer, or null to clear.</param>
/// <param name="AddOnCodes">The requested add-on codes.</param>
/// <param name="ExpectedRevision">The last observed revision.</param>
public sealed record PutBasketCommand(
    string TenantId,
    string SubjectId,
    string? OfferId,
    IReadOnlyList<string> AddOnCodes,
    uint ExpectedRevision) : ICommand<ErrorOr<BasketDto>>, ITenantMessage;
