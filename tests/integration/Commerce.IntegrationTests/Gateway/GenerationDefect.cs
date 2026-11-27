namespace Commerce.IntegrationTests.Gateway;

/// <summary>Invalid active desired generation encodings.</summary>
public enum GenerationDefect
{
    /// <summary>Optional field absent.</summary>
    Absent = 0,

    /// <summary>Optional field explicitly zero.</summary>
    Zero = 1,
}
