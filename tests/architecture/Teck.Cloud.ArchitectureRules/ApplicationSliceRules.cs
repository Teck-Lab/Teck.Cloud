using System.Runtime.CompilerServices;
using FluentValidation;
using SharedKernel.Core.CQRS;
using Xunit;
using Assembly = System.Reflection.Assembly;

namespace Teck.Cloud.ArchitectureRules;

/// <summary>Enforces capability-based vertical slices in a service Application project.</summary>
public static class ApplicationSliceRules
{
    /// <summary>
    /// Requires application types to use the Application root namespace, and requires commands,
    /// queries, validators and their static Wolverine handlers to be colocated at
    /// <c lang="csharp">&lt;Capability&gt;.Features.&lt;UseCase&gt;.V&lt;version&gt;</c>.
    /// </summary>
    /// <param name="applicationAssembly">The service Application assembly.</param>
    /// <param name="applicationNamespace">The namespace root, for example <c lang="csharp">Orders.Application</c>.</param>
    public static void TypesShouldFollowVerticalSlices(Assembly applicationAssembly, string applicationNamespace)
    {
        ArgumentNullException.ThrowIfNull(applicationAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationNamespace);

        var types = GetAuthoredTopLevelTypes(applicationAssembly).ToArray();
        var violations = new List<string>();
        var outsideApplication = types
            .Where(type => !IsInNamespace(type.Namespace, applicationNamespace))
            .Select(FormatType)
            .ToArray();
        violations.AddRange(outsideApplication.Select(type => $"Outside '{applicationNamespace}': {type}"));

        var applicationTypes = types.Where(type => IsInNamespace(type.Namespace, applicationNamespace)).ToArray();
        var misplacedContracts = applicationTypes
            .Where(type => IsCommandOrQuery(type) && !IsFeatureNamespace(type.Namespace!, applicationNamespace))
            .Select(FormatType)
            .ToArray();
        violations.AddRange(misplacedContracts.Select(type => $"Command or query outside its feature slice: {type}"));

        var misplacedValidators = applicationTypes
            .Where(validator => IsValidator(validator)
                && GetValidatedTypes(validator).Any(request =>
                    !string.Equals(request.Namespace, validator.Namespace, StringComparison.Ordinal)
                    || !IsFeatureNamespace(validator.Namespace!, applicationNamespace)))
            .Select(FormatType)
            .ToArray();
        violations.AddRange(misplacedValidators.Select(type => $"Validator outside its request feature slice: {type}"));

        var misplacedHandlers = HandlerReflection.GetHandlerClasses(applicationAssembly)
            .Where(handler =>
                (HandlerReflection.IsCommandHandlerClass(handler) || HandlerReflection.IsQueryHandlerClass(handler))
                && (!IsFeatureNamespace(handler.Namespace ?? string.Empty, applicationNamespace)
                    || HandlerReflection.HandleParameterTypes(handler).Any(request =>
                        IsCommandOrQuery(request)
                        && !string.Equals(request.Namespace, handler.Namespace, StringComparison.Ordinal))))
            .Select(FormatType)
            .ToArray();
        violations.AddRange(misplacedHandlers.Select(type => $"Handler outside its request feature slice: {type}"));

        Assert.True(
            violations.Count == 0,
            "Application types must follow capability feature slices: " + string.Join(", ", violations));
    }

    private static IEnumerable<Type> GetAuthoredTopLevelTypes(Assembly assembly) => assembly.GetTypes()
        .Where(type => !type.IsNested
            && !string.Equals(type.Name, "<Module>", StringComparison.Ordinal)
            && !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false));

    private static IEnumerable<Type> GetValidatedTypes(Type validator) => validator.GetInterfaces()
        .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IValidator<>))
        .Select(contract => contract.GenericTypeArguments[0]);

    private static bool IsValidator(Type type) => GetValidatedTypes(type).Any();

    private static bool IsCommandOrQuery(Type type) => type.GetInterfaces().Any(contract =>
        contract.IsGenericType
        && (contract.GetGenericTypeDefinition() == typeof(ICommand<>) || contract.GetGenericTypeDefinition() == typeof(IQuery<>)));

    private static bool IsInNamespace(string? typeNamespace, string namespaceRoot) =>
        typeNamespace is not null && (string.Equals(typeNamespace, namespaceRoot, StringComparison.Ordinal)
            || typeNamespace.StartsWith(namespaceRoot + ".", StringComparison.Ordinal));

    private static bool IsFeatureNamespace(string typeNamespace, string applicationNamespace)
    {
        if (!typeNamespace.StartsWith(applicationNamespace + ".", StringComparison.Ordinal))
        {
            return false;
        }

        var segments = typeNamespace[(applicationNamespace.Length + 1)..].Split('.');
        if (segments.Length < 4 || !string.Equals(segments[^3], "Features", StringComparison.Ordinal))
        {
            return false;
        }

        var capabilitySegments = segments[..^3];
        var useCase = segments[^2];
        var version = segments[^1];
        return capabilitySegments.Length > 0
            && capabilitySegments.All(IsIdentifier)
            && IsIdentifier(useCase)
            && version.Length > 1
            && version[0] == 'V'
            && version[1] is >= '1' and <= '9'
            && version[2..].All(char.IsAsciiDigit);
    }

    private static bool IsIdentifier(string value) => value.Length > 0
        && (char.IsAsciiLetter(value[0]) || value[0] == '_')
        && value.Skip(1).All(character => char.IsAsciiLetterOrDigit(character) || character == '_');

    private static string FormatType(Type type) => type.FullName ?? type.Name;
}
