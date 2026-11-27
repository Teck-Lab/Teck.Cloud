using System.Reflection;

namespace Orders.Architecture.UnitTests;

/// <summary>Resolves the compiled Orders service assemblies under test.</summary>
internal static class OrdersAssemblies
{
    /// <summary>Root namespace shared by the Orders service layers.</summary>
    internal const string OrdersNamespace = "Teck.Cloud.Commerce.Orders";

    /// <summary>Gets the Orders.Domain assembly.</summary>
    internal static Assembly Domain { get; } = Load("Orders.Domain");

    /// <summary>Gets the Orders.Application assembly.</summary>
    internal static Assembly Application { get; } = Load("Orders.Application");

    /// <summary>Gets the Orders.Infrastructure assembly.</summary>
    internal static Assembly Infrastructure { get; } = Load("Orders.Infrastructure");

    /// <summary>Gets the Orders.Host assembly.</summary>
    internal static Assembly Host { get; } = Load("Orders.Host");

    /// <summary>Gets the four Orders layer assemblies in dependency order.</summary>
    internal static Assembly[] Layers { get; } = [Domain, Application, Infrastructure, Host];

    private static Assembly Load(string assemblyName) => Assembly.Load(new AssemblyName(assemblyName));
}
