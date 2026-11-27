namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>The disposable K1/K2 rotation ring.</summary>
internal sealed class CommerceKeyRing
{
    internal CommerceKey K1 { get; } = CommerceKey.Generate("K1");

    internal CommerceKey K2 { get; } = CommerceKey.Generate("K2");

    internal IReadOnlyList<(string Key, string Value)> Initial()
    {
        var values = new List<(string Key, string Value)>();
        values.AddRange(K1.Signing("0"));
        values.AddRange(K2.PublicOnly("1"));
        return values;
    }

    internal IReadOnlyList<(string Key, string Value)> RotatedToK2()
    {
        var values = new List<(string Key, string Value)>();
        values.AddRange(K1.PublicOnly("0"));
        values.AddRange(K2.Signing("1"));
        return values;
    }

    internal IReadOnlyList<(string Key, string Value)> K2Only()
    {
        var values = new List<(string Key, string Value)>();
        values.AddRange(K2.Signing("0"));
        return values;
    }
}
