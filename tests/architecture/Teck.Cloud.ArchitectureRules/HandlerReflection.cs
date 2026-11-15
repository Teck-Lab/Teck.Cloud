using System.Reflection;
using SharedKernel.Core.CQRS;

namespace Teck.Cloud.ArchitectureRules;

/// <summary>Discovers Wolverine static handlers by their message parameter.</summary>
internal static class HandlerReflection
{
    internal static IEnumerable<Type> GetHandlerClasses(Assembly assembly) =>
        assembly.GetTypes().Where(type => type.IsClass && type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Any(method => IsHandleMethod(method)));

    internal static IEnumerable<MethodInfo> GetHandlerMethods(Assembly assembly) =>
        GetHandlerClasses(assembly).SelectMany(handler => handler.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(IsHandleMethod));

    internal static bool IsCommandHandler(MethodInfo method) =>
        MethodParameterTypes(method).Any(type => ImplementsOpenGeneric(type, typeof(ICommand<>)));

    internal static bool IsQueryHandler(MethodInfo method) =>
        MethodParameterTypes(method).Any(type => ImplementsOpenGeneric(type, typeof(IQuery<>)));

    internal static IEnumerable<Type> MethodParameterTypes(MethodInfo method) =>
        method.GetParameters().Select(parameter => parameter.ParameterType);

    internal static bool HasExactGenericParameter(MethodInfo method, Type openContract) =>
        MethodParameterTypes(method).Any(type => type.IsGenericType && type.GetGenericTypeDefinition() == openContract);

    internal static IEnumerable<Type> HandleParameterTypes(Type handler) =>
        handler.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(IsHandleMethod)
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType);

    internal static bool IsCommandHandlerClass(Type handler) =>
        HandleParameterTypes(handler).Any(type => ImplementsOpenGeneric(type, typeof(ICommand<>)));

    internal static bool IsQueryHandlerClass(Type handler) =>
        HandleParameterTypes(handler).Any(type => ImplementsOpenGeneric(type, typeof(IQuery<>)));

    private static bool IsHandleMethod(MethodInfo method) =>
        string.Equals(method.Name, "Handle", StringComparison.Ordinal)
        || string.Equals(method.Name, "HandleAsync", StringComparison.Ordinal);

    private static bool ImplementsOpenGeneric(Type type, Type openInterface) =>
        (type.IsGenericType && type.GetGenericTypeDefinition() == openInterface)
        || type.GetInterfaces().Any(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == openInterface);
}
