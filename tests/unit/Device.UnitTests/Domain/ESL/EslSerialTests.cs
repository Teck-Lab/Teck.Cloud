// <copyright file="EslSerialTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.ESL;
using Shouldly;

namespace Device.UnitTests.Domain.ESL;

public sealed class EslSerialTests
{
    [Fact]
    public void DeriveShortSerial_ShouldReturnKnownResult_ForDocumentedExample()
    {
        // Arrange — hardware-confirmed: 229582052926557319 mod 10_000_000_000 = 2926557319 = 0xAE6FB887
        const long longSerial = 229582052926557319L;

        // Act
        string shortSerial = EslSerial.DeriveShortSerial(longSerial);

        // Assert
        shortSerial.ShouldBe("AE-6F-B8-87");
    }

    [Fact]
    public void DeriveShortSerial_ShouldReturnZeroedSerial_WhenInputIsZero()
    {
        // Act
        string shortSerial = EslSerial.DeriveShortSerial(0L);

        // Assert
        shortSerial.ShouldBe("00-00-00-00");
    }

    [Fact]
    public void DeriveShortSerial_ShouldReturnCorrectSerial_ForSmallValue()
    {
        // Arrange — 256 = 0x00000100 → "00-00-01-00"
        const long longSerial = 256L;

        // Act
        string shortSerial = EslSerial.DeriveShortSerial(longSerial);

        // Assert
        shortSerial.ShouldBe("00-00-01-00");
    }

    [Fact]
    public void DeriveShortSerial_ShouldApplyModulus_WhenInputExceedsModulusBase()
    {
        // Arrange — 10_000_000_001 mod 10_000_000_000 = 1 → 0x00000001 → "00-00-00-01"
        const long longSerial = 10_000_000_001L;

        // Act
        string shortSerial = EslSerial.DeriveShortSerial(longSerial);

        // Assert
        shortSerial.ShouldBe("00-00-00-01");
    }

    [Fact]
    public void DeriveShortSerial_ShouldReturnUpperCaseHex()
    {
        // Arrange — any value that includes a-f in hex output
        const long longSerial = 229582052926557319L;

        // Act
        string shortSerial = EslSerial.DeriveShortSerial(longSerial);

        // Assert — all hex characters should be uppercase
        shortSerial.ShouldBe(shortSerial.ToUpperInvariant());
    }
}
