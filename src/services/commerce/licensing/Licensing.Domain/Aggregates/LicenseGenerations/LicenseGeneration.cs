using ErrorOr;
using SharedKernel.Core.Domain;

namespace Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGenerations;

/// <summary>Durable fencing row for one tenant/order/subscription binding.</summary>
public sealed class LicenseGeneration : BaseEntity, IAggregateRoot, ITenantScoped
{
    private LicenseGeneration()
    {
    }

    /// <inheritdoc/>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Gets the bound order.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Gets the bound subscription.</summary>
    public Guid SubscriptionId { get; private set; }

    /// <summary>Gets the highest accepted Orders desired generation; zero before the first snapshot.</summary>
    public uint DesiredRevision { get; private set; }

    /// <summary>Gets the last issued signed-license revision; zero before the first active grant.</summary>
    public uint LicenseRevision { get; private set; }

    /// <summary>Gets the application-managed optimistic concurrency fence.</summary>
    public uint Fence { get; private set; }

    /// <summary>Gets the UTC instant of the last accepted snapshot.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Starts a binding that has not yet accepted any generation.</summary>
    /// <param name="tenantId">The immutable organization UUID.</param>
    /// <param name="orderId">The bound order.</param>
    /// <param name="subscriptionId">The bound subscription.</param>
    /// <returns>The unsaved binding row or a stable invariant error.</returns>
    public static ErrorOr<LicenseGeneration> Start(string tenantId, Guid orderId, Guid subscriptionId)
    {
        return !Guid.TryParseExact(tenantId, "D", out var tenant) || orderId == Guid.Empty || subscriptionId == Guid.Empty
            ? LicenseGenerationErrors.InvalidBinding
            : new LicenseGeneration
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.ToString("D"),
                OrderId = orderId,
                SubscriptionId = subscriptionId,
                DesiredRevision = 0,
                LicenseRevision = 0,
                Fence = 0,
                UpdatedAt = DateTimeOffset.UnixEpoch,
            };
    }

    /// <summary>Determines whether a desired generation may replace the current snapshot.</summary>
    /// <param name="desiredRevision">The incoming desired generation.</param>
    /// <returns>True only for a strictly higher positive generation.</returns>
    public bool CanAccept(uint desiredRevision) => desiredRevision > DesiredRevision;

    /// <summary>Computes the next monotonic signed-license revision.</summary>
    /// <returns>The next revision, or null on 32-bit exhaustion.</returns>
    public uint? NextLicenseRevision() => LicenseRevision == uint.MaxValue ? null : LicenseRevision + 1;

    /// <summary>Advances the fence and records the accepted generation.</summary>
    /// <param name="desiredRevision">The accepted generation.</param>
    /// <param name="licenseRevision">The issued license revision, or null for an inactive snapshot.</param>
    /// <param name="utcNow">The verified server UTC instant.</param>
    /// <exception cref="InvalidOperationException">The generation is not strictly higher or the revision regresses.</exception>
    public void Accept(uint desiredRevision, uint? licenseRevision, DateTimeOffset utcNow)
    {
        if (!CanAccept(desiredRevision))
        {
            throw new InvalidOperationException("A desired generation must be strictly increasing.");
        }

        if (licenseRevision is { } issued && (issued <= LicenseRevision || issued == 0))
        {
            throw new InvalidOperationException("A license revision must be positive and monotonic.");
        }

        DesiredRevision = desiredRevision;
        if (licenseRevision is { } revision)
        {
            LicenseRevision = revision;
        }

        Fence = Fence == uint.MaxValue ? 1 : Fence + 1;
        UpdatedAt = utcNow.ToUniversalTime();
    }
}
