using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

namespace Teck.Cloud.Commerce.Orders.Application.Baskets.Features.GetBasket.V1;

/// <summary>Reads the authenticated subject's basket.</summary>
/// <param name="TenantId">The validated tenant id.</param>
/// <param name="SubjectId">The authenticated subject.</param>
public sealed record GetBasketQuery(string TenantId, string SubjectId) : IQuery<ErrorOr<BasketDto>>, ITenantMessage;
