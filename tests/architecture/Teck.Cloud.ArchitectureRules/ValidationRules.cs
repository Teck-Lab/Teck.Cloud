using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using FluentValidation;
using Assembly = System.Reflection.Assembly;

namespace Teck.Cloud.ArchitectureRules;

/// <summary>Validator shape and placement rules.</summary>
public static class ValidationRules
{
    /// <summary>Checks validators should be sealed.</summary>
    /// <param name="architecture">architecture for ValidatorsShouldBeSealed.</param>
    public static void ValidatorsShouldBeSealed(Architecture architecture)
    {
        var rule = ArchRuleDefinition
            .Classes()
            .That()
            .ImplementInterface(typeof(IValidator<>))
            .Should()
            .BeSealed()
            .Because("validators should be sealed to prevent inheritance")
            .WithoutRequiringPositiveResults();

        rule.Check(architecture);
    }

    /// <summary>Checks validators in one service Application assembly are sealed.</summary>
    /// <param name="architecture">Loaded service architecture.</param>
    /// <param name="applicationAssembly">The concrete service Application assembly.</param>
    public static void ValidatorsShouldBeSealed(Architecture architecture, Assembly applicationAssembly)
    {
        var rule = ArchRuleDefinition
            .Classes()
            .That()
            .ResideInAssembly(applicationAssembly)
            .And()
            .ImplementInterface(typeof(IValidator<>))
            .Should()
            .BeSealed()
            .Because("validators should be sealed to prevent inheritance")
            .WithoutRequiringPositiveResults();

        rule.Check(architecture);
    }

    /// <summary>Checks validators should reside in validation namespace.</summary>
    /// <param name="architecture">architecture for ValidatorsShouldResideInValidationNamespace.</param>
    /// <param name="validationNamespace">validationNamespace for ValidatorsShouldResideInValidationNamespace.</param>
    public static void ValidatorsShouldResideInValidationNamespace(Architecture architecture, string validationNamespace)
    {
        var rule = ArchRuleDefinition
            .Classes()
            .That()
            .ImplementInterface(typeof(IValidator<>))
            .Should()
            .ResideInNamespaceMatching(validationNamespace)
            .Because("validators should be organized in validation folders")
            .WithoutRequiringPositiveResults();

        rule.Check(architecture);
    }
}
