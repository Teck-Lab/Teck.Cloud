using System.Reflection;
using System.Reflection.Emit;
using Teck.Cloud.ArchitectureRules;
using Xunit;

namespace SharedKernel.ArchitectureTests;

/// <summary>Proves service application and transport layers cannot reference AutoMapper.</summary>
public sealed class MapperRuleTests
{
    /// <summary>A compiled service assembly reference to AutoMapper is rejected.</summary>
    [Fact]
    public void AutoMapperAssemblyReferenceIsRejected()
    {
        var autoMapperAssembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("AutoMapper"),
            AssemblyBuilderAccess.Run);
        var profileType = autoMapperAssembly
            .DefineDynamicModule("AutoMapper")
            .DefineType("AutoMapper.Profile", TypeAttributes.Public)
            .CreateType();

        var serviceAssembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("MapperRuleFixture.Application"),
            AssemblyBuilderAccess.Run);
        _ = serviceAssembly
            .DefineDynamicModule("MapperRuleFixture.Application")
            .DefineType("MapperRuleFixture.ManualProfile", TypeAttributes.Public, profileType)
            .CreateType();

        var failure = Assert.ThrowsAny<Exception>(() =>
            MapperRules.AutoMapperReferencesShouldNotBeUsed(serviceAssembly));

        Assert.Contains("AutoMapper", failure.Message, StringComparison.Ordinal);
    }
}
