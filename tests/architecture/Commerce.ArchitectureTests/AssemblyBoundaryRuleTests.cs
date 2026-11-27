using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace Commerce.ArchitectureTests;

/// <summary>Proves the assembly boundary mechanism rejects a prohibited direct reference.</summary>
public sealed class AssemblyBoundaryRuleTests
{
    /// <summary>A compiled reference to a prohibited assembly is reported as a violation.</summary>
    [Fact]
    public void ProhibitedDirectReferenceIsDetected()
    {
        var prohibitedAssembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Probe.Prohibited"),
            AssemblyBuilderAccess.Run);
        var prohibitedType = prohibitedAssembly
            .DefineDynamicModule("Probe.Prohibited")
            .DefineType("Probe.Prohibited.Marker", TypeAttributes.Public)
            .CreateType();

        var subjectAssembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Probe.Subject"),
            AssemblyBuilderAccess.Run);
        _ = subjectAssembly
            .DefineDynamicModule("Probe.Subject")
            .DefineType("Probe.Subject.Leak", TypeAttributes.Public, prohibitedType)
            .CreateType();

        var violations = AssemblyBoundaryRules.FindProhibitedReferences(
            [subjectAssembly],
            ["Probe.Prohibited"]);

        Assert.Contains("Probe.Subject -> Probe.Prohibited", violations, StringComparer.Ordinal);
    }

    /// <summary>An unrelated assembly remains clean so the boundary does not fail closed by accident.</summary>
    [Fact]
    public void UnrelatedAssemblyIsNotReported()
    {
        var violations = AssemblyBoundaryRules.FindProhibitedReferences(
            [typeof(AssemblyBoundaryRules).Assembly],
            ["Probe.Prohibited"]);

        Assert.Empty(violations);
    }
}
