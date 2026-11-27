using SharedKernel.Core.Domain;
using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.ReadModels;

/// <summary>Tenant-filtered audit projection.</summary>
public sealed class OrderAuditReadModel : ReadModelBase<Guid>, ITenantScoped
{
    /// <inheritdoc/>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Gets or sets the audited order id.</summary>
    public Guid OrderId { get; set; }

    /// <summary>Gets or sets the staff subject.</summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>Gets or sets the recorded action.</summary>
    public AuditAction Action { get; set; }

    /// <summary>Gets or sets the reason or evidence reference.</summary>
    public string? Reason { get; set; }

    /// <summary>Gets or sets the server UTC instant.</summary>
    public DateTimeOffset OccurredAt { get; set; }
}
