using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

namespace Teck.Cloud.Commerce.Orders.Application.Catalog.Features.GetCatalog.V1;

/// <summary>Reads the currently advertised server-priced catalog.</summary>
/// <param name="TenantId">The validated tenant id.</param>
public sealed record GetCatalogQuery(string TenantId) : IQuery<ErrorOr<IReadOnlyList<OfferDto>>>, ITenantMessage;
