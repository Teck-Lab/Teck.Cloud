namespace SharedKernel.Persistence.Database;

/// <summary>Separates EF configuration discovery for read and write contexts.</summary>
public static class ModelConfigurationFilters
{
    /// <summary>Includes shared mappings and write-side mappings, excluding read projections.</summary>
    /// <param name="configurationType">A type discovered by EF configuration scanning.</param>
    /// <returns>Whether the write context should apply this configuration.</returns>
    public static bool WriteConfigurationsFilter(Type configurationType)
    {
        ArgumentNullException.ThrowIfNull(configurationType);
        return Implements(configurationType, typeof(ISharedModelConfiguration<>))
            || Implements(configurationType, typeof(IWriteModelConfiguration<>));
    }

    /// <summary>Includes shared mappings and read-side mappings, excluding write-only mappings.</summary>
    /// <param name="configurationType">A type discovered by EF configuration scanning.</param>
    /// <returns>Whether the read context should apply this configuration.</returns>
    public static bool ReadConfigurationsFilter(Type configurationType)
    {
        ArgumentNullException.ThrowIfNull(configurationType);
        return Implements(configurationType, typeof(ISharedModelConfiguration<>))
            || Implements(configurationType, typeof(IReadModelConfiguration<>));
    }

    private static bool Implements(Type candidate, Type openMarker) =>
        candidate.GetInterfaces().Any(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == openMarker);
}
