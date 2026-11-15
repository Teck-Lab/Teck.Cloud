using SharedKernel.Persistence.Database;
using Xunit;
using Assembly = System.Reflection.Assembly;

namespace Teck.Cloud.ArchitectureRules;

/// <summary>Conventions for read-side EF models.</summary>
public static class ReadModelRules
{
    /// <summary>Requires mapped read-side types to end in ReadModel.</summary>
    /// <param name="infrastructureAssembly">The service Infrastructure assembly containing EF model mappings.</param>
    public static void ReadModelsShouldHaveExplicitNames(Assembly infrastructureAssembly)
    {
        ArgumentNullException.ThrowIfNull(infrastructureAssembly);

        var names = string.Join(", ", infrastructureAssembly.GetTypes()
            .SelectMany(configuration => configuration.GetInterfaces())
            .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IReadModelConfiguration<>))
            .Select(contract => contract.GenericTypeArguments[0])
            .Where(model => !model.Name.EndsWith("ReadModel", StringComparison.Ordinal))
            .Select(model => model.FullName));
        Assert.True(string.IsNullOrEmpty(names), "Read EF models must end in ReadModel: " + names);
    }
}
