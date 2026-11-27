using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>Admin fulfillment projection.</summary>
/// <param name="Status">The projected fulfillment state.</param>
/// <param name="PlacementId">The current placement id.</param>
/// <param name="EvidenceReference">The current audit reference.</param>
/// <param name="ObservedAt">The fresh success or terminal completion instant.</param>
/// <param name="FailureCode">The current failure classification.</param>
public sealed record FulfillmentDto(
    FulfillmentStatus Status,
    string? PlacementId,
    string? EvidenceReference,
    DateTimeOffset? ObservedAt,
    FulfillmentFailureCode? FailureCode);
