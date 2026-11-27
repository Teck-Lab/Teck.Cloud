using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErrorOr;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Teck.Cloud.Commerce.Orders.Application.Orders.Cursor;
using Teck.Cloud.Commerce.Orders.Application.Orders.Settings;

namespace Teck.Cloud.Commerce.Orders.Infrastructure;

/// <summary>HMAC-protected, scope-bound opaque cursors.</summary>
/// <param name="options">The required signing configuration.</param>
public sealed class HmacOrderCursorCodec(IOptions<OrderCursorOptions> options) : IOrderCursorCodec
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly byte[] _key = Encoding.UTF8.GetBytes(
        !string.IsNullOrWhiteSpace(options.Value.SigningKey) && options.Value.SigningKey.Length >= 32
            ? options.Value.SigningKey
            : throw new InvalidOperationException("CommerceOrders:Cursor:SigningKey with at least 32 characters is required."));

    /// <inheritdoc/>
    public string Encode(OrderCursorScope scope, OrderCursorPosition position)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new CursorPayload(scope.Surface, scope.TenantId, scope.SubjectId, position.UpdatedAt.UtcTicks, position.SortKey, position.OrderId),
            SerializerOptions);
        var signature = HMACSHA256.HashData(_key, payload);
        return WebEncoders.Base64UrlEncode(payload) + "." + WebEncoders.Base64UrlEncode(signature);
    }

    /// <inheritdoc/>
    public ErrorOr<OrderCursorPosition> Decode(OrderCursorScope scope, string cursor)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (string.IsNullOrWhiteSpace(cursor) || cursor.Length > 1024)
        {
            return Invalid();
        }

        var parts = cursor.Split('.');
        if (parts.Length != 2)
        {
            return Invalid();
        }

        byte[] payload;
        byte[] provided;
        try
        {
            payload = WebEncoders.Base64UrlDecode(parts[0]);
            provided = WebEncoders.Base64UrlDecode(parts[1]);
        }
        catch (FormatException)
        {
            return Invalid();
        }

        var expected = HMACSHA256.HashData(_key, payload);
        if (provided.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(provided, expected))
        {
            return Invalid();
        }

        CursorPayload? decoded;
        try
        {
            decoded = JsonSerializer.Deserialize<CursorPayload>(payload, SerializerOptions);
        }
        catch (JsonException)
        {
            return Invalid();
        }

        return decoded is null
            || decoded.Surface != scope.Surface
            || !string.Equals(decoded.TenantId, scope.TenantId, StringComparison.Ordinal)
            || !string.Equals(decoded.SubjectId, scope.SubjectId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(decoded.SortKey)
            || decoded.SortKey.Length > 36
            ? Invalid()
            : new OrderCursorPosition(new DateTimeOffset(decoded.UpdatedAtUtcTicks, TimeSpan.Zero), decoded.SortKey, decoded.OrderId);
    }

    private static Error Invalid() => Error.Validation("invalid_request", "The cursor is invalid for this scope.");

    private sealed record CursorPayload(
        OrderListSurface Surface,
        string TenantId,
        string? SubjectId,
        long UpdatedAtUtcTicks,
        string SortKey,
        Guid OrderId);
}
