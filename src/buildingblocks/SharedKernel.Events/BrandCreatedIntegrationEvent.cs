using SharedKernel.Core.Events;

namespace SharedKernel.Events
{
    /// <summary>
    /// The brand created integration event.
    /// </summary>
    public class BrandCreatedIntegrationEvent : IntegrationEvent
    {
        /// <summary>
        /// Gets or sets the brand id.
        /// </summary>
        public Guid BrandId { get; set; } // Changed to settable for deserialization

        /// <summary>
        /// Initializes a new instance of the <see cref="BrandCreatedIntegrationEvent"/> class.
        /// </summary>
        public BrandCreatedIntegrationEvent()
        {
            // Parameterless constructor for MassTransit serialization
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BrandCreatedIntegrationEvent"/> class.
        /// </summary>
        /// <param name="brandId">The brand id.</param>
        public BrandCreatedIntegrationEvent(Guid brandId)
        {
            BrandId = brandId;
        }
    }
}
