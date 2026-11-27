using System.Security.Cryptography;
using System.Text;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Checkout;

/// <summary>Stable identifiers derived from the immutable checkout intent.</summary>
public static class CheckoutIds
{
    /// <summary>Derives the stable order id for a tenant, subject and idempotency key.</summary>
    /// <param name="tenantId">The validated tenant id.</param>
    /// <param name="subjectId">The authenticated subject.</param>
    /// <param name="idempotencyKey">The canonical key.</param>
    /// <returns>The deterministic order id.</returns>
    public static Guid OrderId(string tenantId, string subjectId, string idempotencyKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        var material = Encoding.UTF8.GetBytes(string.Join('\u001F', "commerce-order.v1", tenantId, subjectId, idempotencyKey));
        var hash = SHA256.HashData(material);
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}
