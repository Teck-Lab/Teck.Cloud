using System.Reflection;

namespace Licensing.Architecture.UnitTests;

/// <summary>Resolves the compiled Licensing service assemblies under test.</summary>
internal static class LicensingAssemblies
{
    internal const string LicensingNamespace = "Teck.Cloud.Commerce.Licensing";

    internal static Assembly Domain { get; } = Load("Teck.Cloud.Commerce.Licensing.Domain");

    internal static Assembly Application { get; } = Load("Teck.Cloud.Commerce.Licensing.Application");

    internal static Assembly Infrastructure { get; } = Load("Teck.Cloud.Commerce.Licensing.Infrastructure");

    internal static Assembly Host { get; } = Load("Teck.Cloud.Commerce.Licensing.Host");

    internal static Assembly[] Layers { get; } = [Domain, Application, Infrastructure, Host];

    private static Assembly Load(string assemblyName) => Assembly.Load(new AssemblyName(assemblyName));
}
