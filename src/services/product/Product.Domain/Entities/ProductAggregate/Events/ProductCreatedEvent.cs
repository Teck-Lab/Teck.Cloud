// <copyright file="ProductCreatedEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Events;

namespace Product.Domain.Entities.ProductAggregate.Events;

/// <summary>
/// Domain event raised when a new product is created.
/// </summary>
/// <param name="ProductId">The product identifier.</param>
/// <param name="Name">The product name.</param>
public sealed class ProductCreatedEvent(Guid ProductId, string Name) : DomainEvent
{
    /// <summary>
    /// Gets the product identifier.
    /// </summary>
    public Guid ProductId { get; } = ProductId;

    /// <summary>
    /// Gets the product name.
    /// </summary>
    public string Name { get; } = Name;
}
