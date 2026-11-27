using ErrorOr;
using SharedKernel.Core.Domain;

namespace Teck.Cloud.Commerce.Orders.Domain.Aggregates.Baskets;

/// <summary>The single mutable basket for one tenant and subject.</summary>
public sealed class Basket : BaseEntity<Guid>, IAggregateRoot, ITenantScoped
{
    private Basket()
    {
    }

    /// <inheritdoc/>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Gets the authenticated subject that owns the basket.</summary>
    public string SubjectId { get; private set; } = string.Empty;

    /// <summary>Gets the optimistic revision, starting at zero for an untouched basket.</summary>
    public uint Revision { get; private set; }

    /// <summary>Gets the selected offer id, or null for an empty basket.</summary>
    public string? OfferId { get; private set; }

    /// <summary>Gets the canonical stored add-on codes.</summary>
    public string AddOnCodesCsv { get; private set; } = string.Empty;

    /// <summary>Gets the snapshot total in minor units, or null for an empty basket.</summary>
    public long? TotalAmountMinor { get; private set; }

    /// <summary>Gets the snapshot currency, or null for an empty basket.</summary>
    public string? TotalCurrency { get; private set; }

    /// <summary>Gets the sorted, deduplicated add-on codes.</summary>
    public IReadOnlyList<string> AddOnCodes => BasketSelection.DecodeAddOnCodes(AddOnCodesCsv);

    /// <summary>Gets the current selection, or null for an empty basket.</summary>
    public BasketSelection? Selection => OfferId is null ? null : new BasketSelection(OfferId, AddOnCodes);

    /// <summary>Gets the current snapshot total, or null for an empty basket.</summary>
    public Common.Money? Total => TotalAmountMinor is { } amount && TotalCurrency is { } currency
        ? Common.Money.Create(amount, currency).Value
        : null;

    /// <summary>Creates the empty revision-zero basket for a validated subject.</summary>
    /// <param name="tenantId">The validated tenant id.</param>
    /// <param name="subjectId">The authenticated subject.</param>
    /// <returns>The new basket.</returns>
    public static Basket CreateEmpty(string tenantId, string subjectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        return new Basket { Id = Guid.NewGuid(), TenantId = tenantId, SubjectId = subjectId, Revision = 0 };
    }

    /// <summary>Replaces the selection with an already-priced canonical selection.</summary>
    /// <param name="selection">The canonical selection.</param>
    /// <param name="total">The server-priced total.</param>
    /// <param name="expectedRevision">The caller's last observed revision.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>Success, or a stable stale-revision or validation error.</returns>
    public ErrorOr<Success> Replace(BasketSelection selection, Common.Money total, uint expectedRevision, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (expectedRevision != Revision)
        {
            return BasketErrors.StaleRevision;
        }

        OfferId = selection.OfferId;
        AddOnCodesCsv = selection.EncodeAddOnCodes();
        TotalAmountMinor = total.AmountMinor;
        TotalCurrency = total.Currency;
        Advance(now);
        return Result.Success;
    }

    /// <summary>Clears the selection while preserving the monotonically increasing revision.</summary>
    /// <param name="expectedRevision">The caller's last observed revision.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>Success, or a stale-revision error.</returns>
    public ErrorOr<Success> Clear(uint expectedRevision, DateTimeOffset now)
    {
        if (expectedRevision != Revision)
        {
            return BasketErrors.StaleRevision;
        }

        ClearSelection(now);
        return Result.Success;
    }

    /// <summary>Clears the selection after a winning checkout in the same transaction.</summary>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>Success.</returns>
    public ErrorOr<Success> ClearAfterCheckout(DateTimeOffset now)
    {
        ClearSelection(now);
        return Result.Success;
    }

    /// <summary>Requires a revision match and a nonempty selection before checkout.</summary>
    /// <param name="expectedRevision">The caller's supplied basket revision.</param>
    /// <returns>The checkout snapshot, or a stable error.</returns>
    public ErrorOr<BasketSelection> RequireCheckoutSelection(uint expectedRevision) =>
        (expectedRevision == Revision, Selection) switch
        {
            (false, _) => BasketErrors.StaleRevision,
            (true, { } selection) => selection,
            (true, null) => BasketErrors.Empty,
        };

    private void ClearSelection(DateTimeOffset now)
    {
        OfferId = null;
        AddOnCodesCsv = string.Empty;
        TotalAmountMinor = null;
        TotalCurrency = null;
        Advance(now);
    }

    private void Advance(DateTimeOffset now)
    {
        Revision = checked(Revision + 1);
        SetUpdatedProperties(now, updatedBy: null);
    }
}
