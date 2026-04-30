// <copyright file="GetByIdSupplierResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Catalog.Application.Suppliers.Features.GetSupplierById.V1;

/// <summary>
/// Response DTO for get supplier by id feature.
/// </summary>
[Serializable]
public class GetByIdSupplierResponse
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
    /// Gets or sets the supplier contact email.
    /// </summary>
    public string? ContactEmail { get; set; }

    /// <summary>
    /// Gets or sets the supplier contact phone.
    /// </summary>
    public string? ContactPhone { get; set; }

    /// <summary>
    /// Gets or sets the supplier contact name.
    /// </summary>
    public string? ContactName { get; set; }

    /// <summary>
    /// Gets or sets the supplier website URL.
    /// </summary>
    public Uri? WebsiteUrl { get; set; }
}
