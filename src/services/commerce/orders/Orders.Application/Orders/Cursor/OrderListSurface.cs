namespace Teck.Cloud.Commerce.Orders.Application.Orders.Cursor;

/// <summary>The list surface a cursor belongs to.</summary>
public enum OrderListSurface
{
    /// <summary>The authenticated customer surface.</summary>
    Customer = 1,

    /// <summary>The authorized staff surface for one selected tenant.</summary>
    Admin = 2,
}
