using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Caching;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;
using Xunit;
using Assembly = System.Reflection.Assembly;

namespace Teck.Cloud.ArchitectureRules;

/// <summary>Conventions for Wolverine query handlers and query contracts.</summary>
public static class QueryHandlerRules
{
    /// <summary>Requires static handler classes with the Handler suffix.</summary>
    /// <param name="applicationAssembly">The service application assembly.</param>
    public static void QueryHandlersShouldBeStaticClassesEndingWithHandler(Assembly applicationAssembly)
    {
        var names = string.Join(", ", HandlerReflection.GetHandlerClasses(applicationAssembly)
            .Where(handler => HandlerReflection.IsQueryHandlerClass(handler)
                && (!handler.IsAbstract || !handler.IsSealed || !handler.Name.EndsWith("Handler", StringComparison.Ordinal)))
            .Select(handler => handler.FullName));
        Assert.True(string.IsNullOrEmpty(names), "Query handlers must be static classes ending in Handler: " + names);
    }

    /// <summary>Requires query handlers to live in a feature slice.</summary>
    /// <param name="applicationAssembly">The service application assembly.</param>
    public static void QueryHandlersShouldResideInFeaturesNamespace(Assembly applicationAssembly)
    {
        var names = string.Join(", ", HandlerReflection.GetHandlerClasses(applicationAssembly)
            .Where(handler => HandlerReflection.IsQueryHandlerClass(handler)
                && handler.Namespace?.Contains(".Features.", StringComparison.Ordinal) != true)
            .Select(handler => handler.FullName));
        Assert.True(string.IsNullOrEmpty(names), "Query handlers must reside under .Features.: " + names);
    }

    /// <summary>Prevents query handlers from injecting a write repository.</summary>
    /// <param name="applicationAssembly">The service application assembly.</param>
    public static void QueryHandlersShouldNotUseWriteRepositories(Assembly applicationAssembly)
    {
        var names = string.Join(", ", HandlerReflection.GetHandlerMethods(applicationAssembly)
            .Where(method => HandlerReflection.IsQueryHandler(method)
                && HandlerReflection.HasExactGenericParameter(method, typeof(IGenericWriteRepository<,>)))
            .Select(method => method.DeclaringType?.FullName));
        Assert.True(string.IsNullOrEmpty(names), "Query handlers must not use a write repository: " + names);
    }

    /// <summary>Prevents query handlers from injecting a unit of work or EF context.</summary>
    /// <param name="applicationAssembly">The service application assembly.</param>
    public static void QueryHandlersShouldNotUseMutablePersistence(Assembly applicationAssembly)
    {
        var names = string.Join(", ", HandlerReflection.GetHandlerMethods(applicationAssembly)
            .Where(method => HandlerReflection.IsQueryHandler(method)
                && (HandlerReflection.MethodParameterTypes(method)
                    .Any(type => typeof(IUnitOfWork).IsAssignableFrom(type) || typeof(DbContext).IsAssignableFrom(type))
                    || HandlerReflection.HasExactGenericParameter(method, typeof(IGenericCacheService<,>))))
            .Select(method => method.DeclaringType?.FullName));
        Assert.True(string.IsNullOrEmpty(names), "Query handlers must not use a unit of work, DbContext or mutable cache: " + names);
    }

    /// <summary>Requires each query handler method to inject a read repository or read-only cache.</summary>
    /// <param name="applicationAssembly">The service application assembly.</param>
    public static void QueryHandlersShouldUseReadRepositoriesOrCaches(Assembly applicationAssembly)
    {
        var names = string.Join(", ", HandlerReflection.GetHandlerMethods(applicationAssembly)
            .Where(method => HandlerReflection.IsQueryHandler(method)
                && !HandlerReflection.HasExactGenericParameter(method, typeof(IGenericReadRepository<,>))
                && !HandlerReflection.HasExactGenericParameter(method, typeof(IReadCacheService<,>)))
            .Select(method => method.DeclaringType?.FullName));
        Assert.True(string.IsNullOrEmpty(names), "Query handlers must inject a read repository or read-only cache: " + names);
    }

    /// <summary>Uses ArchUnitNET to require immutable query contracts.</summary>
    /// <param name="architecture">Loaded service architecture.</param>
    public static void QueriesShouldNotModifyState(Architecture architecture) =>
        ArchRuleDefinition.Classes().That().AreAssignableTo(typeof(IQuery<>))
            .Should().BeImmutable().WithoutRequiringPositiveResults().Check(architecture);
}
