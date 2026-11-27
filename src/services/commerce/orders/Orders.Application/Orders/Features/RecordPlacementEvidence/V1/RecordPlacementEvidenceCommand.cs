using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.RecordPlacementEvidence.V1;

/// <summary>Records allowlisted placement evidence and resets verification for the new revision.</summary>
/// <param name="TenantId">The selected tenant id.</param>
/// <param name="OrderId">The order id.</param>
/// <param name="PlacementId">The allowlisted placement id.</param>
/// <param name="EvidenceReference">The bounded nonsecret audit reference.</param>
/// <param name="ExpectedRevision">The expected order revision.</param>
/// <param name="ActorId">The authenticated staff subject.</param>
public sealed record RecordPlacementEvidenceCommand(
    string TenantId,
    Guid OrderId,
    string PlacementId,
    string EvidenceReference,
    uint ExpectedRevision,
    string ActorId) : ICommand<ErrorOr<AdminOrderDto>>, ITenantMessage;
