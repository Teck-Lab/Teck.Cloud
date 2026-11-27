namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>The explicit disposable-copy corruption selected for one startup negative control.</summary>
/// <param name="Kind">The selected corruption kind.</param>
/// <param name="Host">The host that must fail to start.</param>
/// <param name="Root">Absolute root of the prepared candidate copy.</param>
/// <param name="PristineRoot">Absolute root of the untouched same-revision candidate copy.</param>
internal sealed record NegativeSelection(string Kind, CommerceStaticHost Host, string Root, string PristineRoot);
