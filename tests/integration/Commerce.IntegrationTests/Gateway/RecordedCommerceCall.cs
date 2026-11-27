namespace Commerce.IntegrationTests.Gateway;

/// <summary>One observed downstream call; metadata is preserved in order for duplicate-header inspection.</summary>
internal sealed record RecordedCommerceCall
{
    internal required string Method { get; init; }

    internal string? TenantId { get; init; }

    internal string? OrderId { get; init; }

    internal string? SubscriptionId { get; init; }

    internal uint? DesiredRevision { get; init; }

    internal uint? Limit { get; init; }

    internal string? Cursor { get; init; }

    internal uint? BasketRevision { get; init; }

    internal string? IdempotencyKey { get; init; }

    internal IReadOnlyList<KeyValuePair<string, string>> Metadata { get; init; } = [];

    /// <inheritdoc/>
    public override string ToString() => "RecordedCommerceCall { Method = " + Method + ", OrderId = " + OrderId + " }";

    internal string? MetadataValue(string key) =>
        Metadata.FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
}
