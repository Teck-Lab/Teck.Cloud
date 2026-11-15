using System.Reflection;
using System.Runtime.CompilerServices;
using SharedKernel.Core.Domain;
using Xunit;

namespace Teck.Cloud.ArchitectureRules;

/// <summary>Enforces aggregate-first namespaces in a service Domain project.</summary>
public static class AggregateNamespaceRules
{
    private static readonly HashSet<string> TechnicalBuckets = new(StringComparer.Ordinal)
    {
        "Aggregates",
        "Entities",
        "Errors",
        "Events",
        "DomainEvents",
        "Services",
        "ValueObjects",
    };

    /// <summary>
    /// Requires Domain types under <c lang="csharp">&lt;Domain&gt;.Aggregates.&lt;Aggregate&gt;</c>
    /// or the explicit <c lang="csharp">&lt;Domain&gt;.Common</c> namespace, and requires each
    /// aggregate root itself to live directly in its aggregate namespace.
    /// </summary>
    /// <param name="domainAssembly">The service Domain assembly.</param>
    /// <param name="domainNamespace">The Domain root namespace, for example <c lang="csharp">Orders.Domain</c>.</param>
    public static void TypesShouldBeGroupedByAggregate(Assembly domainAssembly, string domainNamespace)
    {
        ArgumentNullException.ThrowIfNull(domainAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(domainNamespace);

        var domainTypes = GetAuthoredTopLevelTypes(domainAssembly).ToArray();
        var outsideDomain = domainTypes
            .Where(type => !IsInNamespace(type.Namespace, domainNamespace))
            .Select(FormatType)
            .ToArray();
        Assert.True(
            outsideDomain.Length == 0,
            $"Domain types must be under '{domainNamespace}': {string.Join(", ", outsideDomain)}");

        var scopedTypes = domainTypes.Where(type => IsInNamespace(type.Namespace, domainNamespace)).ToArray();
        var aggregateRoots = scopedTypes.Where(type => typeof(IAggregateRoot).IsAssignableFrom(type)).ToArray();
        Assert.True(aggregateRoots.Length > 0, $"Domain assembly '{domainAssembly.GetName().Name}' must define an aggregate root.");

        var misplacedRoots = aggregateRoots
            .Where(type => !IsDirectAggregateNamespace(type.Namespace!, domainNamespace))
            .Select(FormatType)
            .ToArray();
        Assert.True(
            misplacedRoots.Length == 0,
            $"Aggregate roots must live directly in '<Domain>.Aggregates.<Aggregate>': {string.Join(", ", misplacedRoots)}");

        var aggregateGroups = aggregateRoots
            .Where(type => IsDirectAggregateNamespace(type.Namespace!, domainNamespace))
            .Select(type => GetAggregateSegment(type.Namespace!, domainNamespace))
            .ToArray();
        var duplicateGroups = aggregateGroups
            .GroupBy(segment => segment, StringComparer.Ordinal)
            .Where(group => group.Skip(1).Any())
            .Select(group => group.Key)
            .ToArray();
        Assert.True(
            duplicateGroups.Length == 0,
            "Each aggregate namespace should have exactly one aggregate root: " + string.Join(", ", duplicateGroups));

        var misplacedTypes = scopedTypes
            .Where(type => !HasAggregateNamespace(type.Namespace!, domainNamespace)
                || (!IsCommonNamespace(type.Namespace!, domainNamespace)
                    && !aggregateGroups.Contains(GetAggregateSegment(type.Namespace!, domainNamespace), StringComparer.Ordinal)))
            .Select(FormatType)
            .ToArray();
        Assert.True(
            misplacedTypes.Length == 0,
            $"Domain types must be grouped under '<Domain>.Aggregates.<Aggregate>' or '{domainNamespace}.Common': {string.Join(", ", misplacedTypes)}");
    }

    private static IEnumerable<Type> GetAuthoredTopLevelTypes(Assembly assembly) => assembly.GetTypes()
        .Where(type => !type.IsNested
            && !string.Equals(type.Name, "<Module>", StringComparison.Ordinal)
            && !IsGeneratedType(type));

    /// <summary>Identifies compiler-generated types and the Wolverine/JasperFx generated model namespace.</summary>
    /// <param name="type">The Domain assembly type to inspect.</param>
    private static bool IsGeneratedType(Type type) =>
        type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
        || (type.Namespace is not null
            && (string.Equals(type.Namespace, "JasperFx.Generated", StringComparison.Ordinal)
                || type.Namespace.StartsWith("JasperFx.Generated.", StringComparison.Ordinal)));

    private static bool IsInNamespace(string? typeNamespace, string namespaceRoot) =>
        typeNamespace is not null && (string.Equals(typeNamespace, namespaceRoot, StringComparison.Ordinal)
            || typeNamespace.StartsWith(namespaceRoot + ".", StringComparison.Ordinal));

    private static bool HasAggregateNamespace(string typeNamespace, string namespaceRoot) =>
        IsCommonNamespace(typeNamespace, namespaceRoot) || IsUnderAggregateNamespace(typeNamespace, namespaceRoot);

    private static bool IsUnderAggregateNamespace(string typeNamespace, string namespaceRoot)
    {
        if (!typeNamespace.StartsWith(namespaceRoot + ".", StringComparison.Ordinal))
        {
            return false;
        }

        var segments = typeNamespace[(namespaceRoot.Length + 1)..].Split('.');
        return segments.Length >= 2
            && string.Equals(segments[0], "Aggregates", StringComparison.Ordinal)
            && !TechnicalBuckets.Contains(segments[1]);
    }

    private static bool IsCommonNamespace(string typeNamespace, string namespaceRoot) =>
        string.Equals(typeNamespace, namespaceRoot + ".Common", StringComparison.Ordinal)
        || typeNamespace.StartsWith(namespaceRoot + ".Common.", StringComparison.Ordinal);

    private static string GetAggregateSegment(string typeNamespace, string namespaceRoot) =>
        typeNamespace[(namespaceRoot.Length + 1)..].Split('.')[1];

    private static bool IsDirectAggregateNamespace(string typeNamespace, string namespaceRoot)
    {
        if (!typeNamespace.StartsWith(namespaceRoot + ".", StringComparison.Ordinal))
        {
            return false;
        }

        var segments = typeNamespace[(namespaceRoot.Length + 1)..].Split('.');
        return segments.Length == 2
            && string.Equals(segments[0], "Aggregates", StringComparison.Ordinal)
            && !TechnicalBuckets.Contains(segments[1]);
    }

    private static string FormatType(Type type) => type.FullName ?? type.Name;
}
