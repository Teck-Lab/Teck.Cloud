using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.MultiTenancy;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.ApplyProbeOutcome.V1;

/// <summary>Applies one leased probe outcome with generation and lease fences.</summary>
/// <param name="TenantId">The validated tenant id.</param>
/// <param name="OrderId">The order id.</param>
/// <param name="WorkId">The probe-work row id.</param>
/// <param name="LeaseToken">The claimed lease token.</param>
/// <param name="EvidenceRevision">The verified evidence revision.</param>
/// <param name="PlacementId">The verified placement id.</param>
/// <param name="Kind">The outcome classification.</param>
/// <param name="ObservedAt">The probe completion instant.</param>
/// <param name="NextDueAt">The next refresh or retry instant for nonterminal outcomes.</param>
public sealed record ApplyProbeOutcomeCommand(
    string TenantId,
    Guid OrderId,
    Guid WorkId,
    Guid LeaseToken,
    uint EvidenceRevision,
    string PlacementId,
    ProbeOutcomeKind Kind,
    DateTimeOffset ObservedAt,
    DateTimeOffset NextDueAt) : ICommand<ErrorOr<bool>>, ITenantMessage;
