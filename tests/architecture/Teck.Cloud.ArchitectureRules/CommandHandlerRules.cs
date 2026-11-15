using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;
using Xunit;
using Assembly = System.Reflection.Assembly;

namespace Teck.Cloud.ArchitectureRules;

/// <summary>Conventions for Wolverine command handlers and command contracts.</summary>
public static class CommandHandlerRules
{
    /// <summary>Requires static handler classes with the Handler suffix.</summary>
    /// <param name="applicationAssembly">The service application assembly.</param>
    public static void CommandHandlersShouldBeStaticClassesEndingWithHandler(Assembly applicationAssembly)
    {
        var names = string.Join(", ", HandlerReflection.GetHandlerClasses(applicationAssembly)
            .Where(handler => HandlerReflection.IsCommandHandlerClass(handler)
                && (!handler.IsAbstract || !handler.IsSealed || !handler.Name.EndsWith("Handler", StringComparison.Ordinal)))
            .Select(handler => handler.FullName));
        Assert.True(string.IsNullOrEmpty(names), "Command handlers must be static classes ending in Handler: " + names);
    }

    /// <summary>Requires command handlers to live in a feature slice.</summary>
    /// <param name="applicationAssembly">The service application assembly.</param>
    public static void CommandHandlersShouldResideInFeaturesNamespace(Assembly applicationAssembly)
    {
        var names = string.Join(", ", HandlerReflection.GetHandlerClasses(applicationAssembly)
            .Where(handler => HandlerReflection.IsCommandHandlerClass(handler)
                && handler.Namespace?.Contains(".Features.", StringComparison.Ordinal) != true)
            .Select(handler => handler.FullName));
        Assert.True(string.IsNullOrEmpty(names), "Command handlers must reside under .Features.: " + names);
    }

    /// <summary>Prevents command handlers from injecting a read repository.</summary>
    /// <param name="applicationAssembly">The service application assembly.</param>
    public static void CommandHandlersShouldNotUseReadRepositories(Assembly applicationAssembly)
    {
        var names = string.Join(", ", HandlerReflection.GetHandlerMethods(applicationAssembly)
            .Where(method => HandlerReflection.IsCommandHandler(method)
                && HandlerReflection.HasExactGenericParameter(method, typeof(IGenericReadRepository<,>)))
            .Select(method => method.DeclaringType?.FullName));
        Assert.True(string.IsNullOrEmpty(names), "Command handlers must load through the write repository: " + names);
    }

    /// <summary>Requires each command handler method to inject a write repository.</summary>
    /// <param name="applicationAssembly">The service application assembly.</param>
    public static void CommandHandlersShouldUseWriteRepositories(Assembly applicationAssembly)
    {
        var names = string.Join(", ", HandlerReflection.GetHandlerMethods(applicationAssembly)
            .Where(method => HandlerReflection.IsCommandHandler(method)
                && !HandlerReflection.HasExactGenericParameter(method, typeof(IGenericWriteRepository<,>)))
            .Select(method => method.DeclaringType?.FullName));
        Assert.True(string.IsNullOrEmpty(names), "Command handlers must inject a write repository: " + names);
    }

    /// <summary>Uses ArchUnitNET to require immutable command contracts.</summary>
    /// <param name="architecture">Loaded service architecture.</param>
    public static void CommandsShouldBeImmutable(Architecture architecture) =>
        ArchRuleDefinition.Classes().That().AreAssignableTo(typeof(ICommand<>))
            .Should().BeImmutable().WithoutRequiringPositiveResults().Check(architecture);
}
