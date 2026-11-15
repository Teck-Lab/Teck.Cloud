using Xunit;
using Assembly = System.Reflection.Assembly;

namespace Teck.Cloud.ArchitectureRules;

/// <summary>Forbids AutoMapper dependencies in service layers.</summary>
public static class MapperRules
{
    /// <summary>Rejects AutoMapper references from the service Domain, Application, Infrastructure and Host assemblies.</summary>
    /// <param name="serviceAssemblies">The service layer assemblies to inspect.</param>
    /// <exception cref="ArgumentNullException">Thrown when the assembly array is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the assembly array contains a null entry.</exception>
    public static void AutoMapperReferencesShouldNotBeUsed(params Assembly[] serviceAssemblies)
    {
        ArgumentNullException.ThrowIfNull(serviceAssemblies);

        var violations = new List<string>();
        foreach (var assembly in serviceAssemblies)
        {
            if (assembly is null)
            {
                throw new ArgumentException("The service assembly list cannot contain null elements.", nameof(serviceAssemblies));
            }

            var autoMapperReferences = assembly.GetReferencedAssemblies()
                .Where(reference => reference.Name is { } name
                    && (string.Equals(name, "AutoMapper", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("AutoMapper.", StringComparison.OrdinalIgnoreCase)))
                .Select(reference => reference.Name)
                .ToArray();
            violations.AddRange(autoMapperReferences.Select(name => $"{assembly.GetName().Name} references prohibited assembly {name}"));
        }

        var message = "Service layer assemblies must not reference AutoMapper: " + string.Join(", ", violations);
        Assert.True(violations.Count == 0, message);
    }
}
