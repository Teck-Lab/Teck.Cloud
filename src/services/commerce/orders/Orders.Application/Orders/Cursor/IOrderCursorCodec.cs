using ErrorOr;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Cursor;

/// <summary>Opaque, scoped, tamper-evident paging cursors.</summary>
public interface IOrderCursorCodec
{
    /// <summary>Encodes a position for the exact scope.</summary>
    /// <param name="scope">The authorization scope.</param>
    /// <param name="position">The last item position.</param>
    /// <returns>The opaque cursor.</returns>
    string Encode(OrderCursorScope scope, OrderCursorPosition position);

    /// <summary>Decodes and validates a cursor for the exact scope.</summary>
    /// <param name="scope">The authorization scope.</param>
    /// <param name="cursor">The supplied opaque cursor.</param>
    /// <returns>The position, or a stable invalid_request error.</returns>
    ErrorOr<OrderCursorPosition> Decode(OrderCursorScope scope, string cursor);
}
