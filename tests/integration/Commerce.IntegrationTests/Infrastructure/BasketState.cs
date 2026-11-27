namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>A persisted basket after a canonicalizing write.</summary>
/// <param name="Revision">The new revision.</param>
/// <param name="TotalMinor">The server total.</param>
/// <param name="Selected">Whether the selection is the requested offer.</param>
internal sealed record BasketState(uint Revision, long TotalMinor, bool Selected);
