using SharedKernel.Core.Events;

namespace Catalog.Domain.Entities.ProductAggregate.Events
{
    /// <summary>
    /// The brand created domain event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ProductCreatedDomainEvent"/> class.
    /// </remarks>
    /// <param name="productId">The brand id.</param>
    /// <param name="name">The brand name.</param>
    public sealed class ProductCreatedDomainEvent(Guid productId, string name) : DomainEvent
    {
        /// <summary>
        /// Gets the brand id.
        /// </summary>
        public Guid ProductId { get; } = productId;

        /// <summary>
        /// Gets the brand name.
        /// </summary>
        public string Name { get; } = name;
    }
}
