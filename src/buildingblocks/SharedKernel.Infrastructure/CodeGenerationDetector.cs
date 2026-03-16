using System.Reflection;

namespace SharedKernel.Infrastructure;

/// <summary>
/// Detects build-time application execution modes such as Wolverine code generation.
/// </summary>
public static class CodeGenerationDetector
{
    /// <summary>
    /// Returns true when the current process is running source generation instead of the normal application host.
    /// </summary>
    public static bool IsRunningGeneration()
    {
        string? entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
        string[] commandLineArgs = Environment.GetCommandLineArgs();

        return string.Equals(entryAssemblyName, "GetDocument.Insider", StringComparison.Ordinal)
            || commandLineArgs.Any(arg => string.Equals(arg, "codegen", StringComparison.OrdinalIgnoreCase));
    }
}
