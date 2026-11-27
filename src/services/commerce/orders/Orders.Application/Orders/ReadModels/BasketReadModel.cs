using SharedKernel.Core.Domain;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.ReadModels;

/// <summary>Tenant-filtered basket projection.</summary>
public sealed class BasketReadModel : ReadModelBase<Guid>, ITenantScoped
{
    /// <inheritdoc/>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Gets or sets the owning subject.</summary>
    public string SubjectId { get; set; } = string.Empty;

    /// <summary>Gets or sets the basket revision.</summary>
    public uint Revision { get; set; }

    /// <summary>Gets or sets the selected offer id.</summary>
    public string? OfferId { get; set; }

    /// <summary>Gets or sets the canonical stored add-on codes.</summary>
    public string AddOnCodesCsv { get; set; } = string.Empty;

    /// <summary>Gets or sets the snapshot total in minor units.</summary>
    public long? TotalAmountMinor { get; set; }

    /// <summary>Gets or sets the snapshot currency.</summary>
    public string? TotalCurrency { get; set; }
}
