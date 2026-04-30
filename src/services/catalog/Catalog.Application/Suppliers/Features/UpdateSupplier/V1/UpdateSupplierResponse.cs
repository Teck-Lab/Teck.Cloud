// <copyright file="UpdateSupplierResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Catalog.Application.Suppliers.Features.UpdateSupplier.V1;

/// <summary>
/// Response DTO for update supplier feature.
/// </summary>
[Serializable]
public class UpdateSupplierResponse
{
    /// <summary>
    /// Gets or sets the supplier identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the supplier name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the supplier description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the supplier website.
    /// </summary>
    public string? Website { get; set; }
}
