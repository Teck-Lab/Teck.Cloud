namespace Commerce.IntegrationTests.Gateway;

/// <summary>Whether an invalid active desired generation is in the initial snapshot or its reread.</summary>
public enum GenerationSnapshot
{
    /// <summary>The initial Orders snapshot lacks or zeroes the desired generation.</summary>
    Initial = 0,

    /// <summary>The still-active reread lacks or zeroes the desired generation.</summary>
    Reread = 1,
}
