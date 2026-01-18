using SharedKernel.Core.Events;

namespace Catalog.Domain.Entities.BrandAggregate.Events
{
    /// <summary>
    /// The brand created domain event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="BrandCreatedDomainEvent"/> class.
    /// </remarks>
    /// <param name="brandId">The brand id.</param>
    /// <param name="brandName">The brand name.</param>
    public sealed class BrandCreatedDomainEvent(Guid brandId, string brandName) : DomainEvent
    {
        /// <summary>
        /// Gets the brand id.
        /// </summary>
        public Guid BrandId { get; } = brandId;

        /// <summary>
        /// Gets the brand name.
        /// </summary>
        public string BrandName { get; } = brandName;
    }
}
