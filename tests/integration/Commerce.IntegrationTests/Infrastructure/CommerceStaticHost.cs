namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>One host's generated registry, static-code expectations and listener addresses.</summary>
internal sealed class CommerceStaticHost
{
    internal CommerceStaticHost(
        string name,
        string generatedRelativePath,
        string handlerTypeName,
        Type owningType,
        IReadOnlyList<Type> allowedServiceLocations)
    {
        Name = name;
        GeneratedRelativePath = generatedRelativePath;
        HandlerTypeName = handlerTypeName;
        OwningType = owningType;
        AllowedServiceLocations = allowedServiceLocations;
    }

    internal string Name { get; }

    /// <summary>Gets the host-relative directory holding the committed <c language="text">codegen write</c> output.</summary>
    internal string GeneratedRelativePath { get; }

    /// <summary>Gets the application handler type the generated adapter invokes.</summary>
    internal string HandlerTypeName { get; }

    internal Type OwningType { get; }

    internal string OwningAssemblyName => OwningType.Assembly.GetName().Name
        ?? throw new InvalidOperationException("The " + Name + " host assembly has no simple name.");

    internal IReadOnlyList<Type> AllowedServiceLocations { get; }
}
