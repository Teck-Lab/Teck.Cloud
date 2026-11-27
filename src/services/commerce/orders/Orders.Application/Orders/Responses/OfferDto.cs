using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>Wire catalog offer.</summary>
/// <param name="OfferId">The opaque offer id.</param>
/// <param name="Tier">The offering tier.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="BasePrice">The monthly base price.</param>
/// <param name="AddOns">The advertised add-ons.</param>
public sealed record OfferDto(string OfferId, Tier Tier, string DisplayName, MoneyDto BasePrice, IReadOnlyList<AddOnDto> AddOns);
