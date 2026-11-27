using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;
using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Catalog;

/// <summary>A server-priced selection.</summary>
/// <param name="Tier">The resolved tier.</param>
/// <param name="Total">The snapshot total.</param>
/// <param name="Price">The provider price request lines.</param>
public sealed record ResolvedSelection(Tier Tier, Money Total, BillingPrice Price);
