// <copyright file="DisplayInkColor.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Domain.Entities.DeviceDefinitionAggregate;

/// <summary>
/// Represents the set of ink colours supported by an e-ink display panel.
/// Multiple values can be combined using bitwise OR.
/// </summary>
[Flags]
public enum DisplayInkColor
{
    /// <summary>No colour information specified.</summary>
    None = 0,

    /// <summary>Black ink layer.</summary>
    Black = 1,

    /// <summary>White background layer.</summary>
    White = 2,

    /// <summary>Red accent ink layer.</summary>
    Red = 4,

    /// <summary>Yellow accent ink layer.</summary>
    Yellow = 8,

    /// <summary>Orange ink layer (rendered by mixing red and yellow layers).</summary>
    Orange = 16,

    /// <summary>Green ink layer.</summary>
    Green = 32,
}
