using System.Reflection;

namespace Commerce.ArchitectureTests;

/// <summary>Inspects direct assembly metadata references for prohibited project or package dependencies.</summary>
internal static class AssemblyBoundaryRules
{
    /// <summary>
    /// Returns one description per direct metadata reference from a subject assembly to a prohibited
    /// assembly name. Transitive references are intentionally not reported: a shared host composition
    /// may legitimately pull packages through a referenced library.
    /// </summary>
    /// <param name="subjects">Assemblies whose direct references are inspected.</param>
    /// <param name="prohibitedNames">Simple assembly names that must not be referenced directly.</param>
    /// <returns>Human-readable violations; an empty array when every subject is clean.</returns>
    internal static string[] FindProhibitedReferences(
        IReadOnlyCollection<Assembly> subjects,
        IReadOnlyCollection<string> prohibitedNames)
    {
        var bannedNames = prohibitedNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return
        [
            .. subjects.SelectMany(subject => subject.GetReferencedAssemblies()
                .Where(reference => reference.Name is { } name && bannedNames.Contains(name))
                .Select(reference => $"{subject.GetName().Name} -> {reference.Name}")),
        ];
    }
}
