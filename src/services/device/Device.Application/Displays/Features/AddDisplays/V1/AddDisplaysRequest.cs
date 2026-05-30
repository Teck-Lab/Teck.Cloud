// <copyright file="AddDisplaysRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.Displays.Features.AddDisplays.V1;

/// <summary>
/// A single display entry in a batch add request.
/// </summary>
/// <param name="ShortSerial">4-byte serial in XX-XX-XX-XX format.</param>
public sealed record AddDisplayItem(string ShortSerial);

/// <summary>
/// Request to register one or more displays to a location node.
/// </summary>
public sealed class AddDisplaysRequest
{
    /// <summary>
    /// Gets or sets the location node the displays will be assigned to.
    /// </summary>
    public string LocationNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of displays to register.
    /// </summary>
    public IReadOnlyList<AddDisplayItem> Displays { get; set; } = [];

    /// <summary>
    /// Gets or sets an optional device definition (model) to assign to all displays.
    /// </summary>
    public Guid? DeviceDefinitionId { get; set; }
}
