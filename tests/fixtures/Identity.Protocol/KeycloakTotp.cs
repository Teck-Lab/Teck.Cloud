using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Identity.Protocol;

internal static class KeycloakTotp
{
    internal static string Create(string secret)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), counter);
        var offset = hash[^1] & 15;
        var value = (BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(offset, 4)) & 0x7fffffff) % 1000000;
        return value.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }
}
