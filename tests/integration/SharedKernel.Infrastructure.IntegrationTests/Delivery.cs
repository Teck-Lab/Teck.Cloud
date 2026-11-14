namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>A transactionally recorded broker delivery.</summary>
public sealed class Delivery
{
    /// <summary>Gets or sets its business idempotency key.</summary>
    public Guid Id { get; set; }
}
