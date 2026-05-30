// <copyright file="EslSerial.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Buffers.Binary;

namespace Device.Domain.ESL;

/// <summary>
/// Utility for Hanshow ESL serial number derivation.
/// </summary>
/// <remarks>
/// Serial derivation formula (confirmed from hardware):
///   longSerial mod 10_000_000_000 → uint32 big-endian → uppercase hex XX-XX-XX-XX
///
/// Example: 229582052926557319 mod 10_000_000_000 = 2926557319 → 0xAE6FB887 → "AE-6F-B8-87".
/// </remarks>
public static class EslSerial
{
    private const long Modulus = 10_000_000_000L;

    /// <summary>
    /// Derives the 4-byte short serial from a long (decimal) serial number.
    /// </summary>
    /// <param name="longSerial">The decimal serial number stamped on the device.</param>
    /// <returns>The short serial in the form <c>XX-XX-XX-XX</c> (uppercase hex).</returns>
    public static string DeriveShortSerial(long longSerial)
    {
        uint value = (uint)(longSerial % Modulus);
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return $"{bytes[0]:X2}-{bytes[1]:X2}-{bytes[2]:X2}-{bytes[3]:X2}";
    }
}
