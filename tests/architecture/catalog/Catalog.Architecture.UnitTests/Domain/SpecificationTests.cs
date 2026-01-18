using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using Ardalis.Specification;

namespace Catalog.Arch.UnitTests.Domain
{
    public class SpecificationTests : ArchUnitBaseTest
    {
        [Fact]
        public void DomainSpecifications_Should_ImplementISpecification()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .ResideInNamespaceMatching(@"^.*\.Domain\..*\.Specifications$")
                .Should()
                .ImplementInterface(typeof(ISpecification<>))
                .Because("domain specifications should implement ISpecification<T>")
                .Check(Architecture);
        }

        [Fact]
        public void ApplicationSpecifications_Should_ImplementISpecification()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .ResideInNamespaceMatching(@"^.*\.Application\..*\.Specifications$")
                .Should()
                .ImplementInterface(typeof(ISpecification<>))
                .Because("application specifications should implement ISpecification<T>")
                .Check(Architecture);
        }

        [Fact]
        public void DomainSpecifications_Should_NotReferenceReadModels()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .ResideInNamespaceMatching(@"^.*\.Domain\..*\.Specifications$")
                .Should()
                .NotDependOnAny(ArchRuleDefinition.Classes().That().ResideInNamespaceMatching(@"^.*\.ReadModels$"))
                .Because("domain specifications should only reference domain entities, not read models")
                .Check(Architecture);
        }

        [Fact]
        public void Specifications_Should_BeSealed()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .ImplementInterface(typeof(ISpecification<>))
                .Should()
                .BeSealed()
                .Because("specifications should be sealed to prevent inheritance")
                .Check(Architecture);
        }

        [Fact]
        public void EntitySpecifications_Should_BeInDomainNamespace()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .ImplementInterface(typeof(ISpecification<>))
                .And()
                .HaveNameEndingWith("Specification")
                .And()
                .DoNotResideInNamespaceMatching(@"^.*\.Application\..*")
                .Should()
                .ResideInNamespaceMatching(@"^.*\.Domain\..*")
                .Because("entity specifications should be in the domain layer")
                .Check(Architecture);
        }

        [Fact]
        public void ReadModelSpecifications_Should_BeInApplicationNamespace()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .ImplementInterface(typeof(ISpecification<>))
                .And()
                .DependOnAny(ArchRuleDefinition.Classes().That().ResideInNamespaceMatching(@"^.*\.ReadModels$"))
                .Should()
                .ResideInNamespaceMatching(@"^.*\.Application\..*")
                .Because("read model specifications should be in the application layer")
                .Check(Architecture);
        }

        [Fact]
        public void SpecificationsForDomainEntities_Should_NotBeInApplicationLayer()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .ResideInNamespaceMatching(@"^.*\.Application\..*")
                .And()
                .ImplementInterface(typeof(ISpecification<>))
                .Should()
                .NotDependOnAny(ArchRuleDefinition.Classes().That().ResideInNamespaceMatching(@"^.*\.Domain\.Entities\..*"))
                .OrShould()
                .DependOnAny(ArchRuleDefinition.Classes().That().ResideInNamespaceMatching(@"^.*\.ReadModels$"))
                .Because("application layer specifications should operate on read models, not directly on domain entities")
                .Check(Architecture);
        }

        [Fact]
        public void EvaluatorSpecifications_Should_BeInInfrastructureLayer()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .ImplementInterface(typeof(Ardalis.Specification.ISpecificationEvaluator))
                .Should()
                .ResideInNamespaceMatching(@"^.*\.Infrastructure\..*")
                .Because("specification evaluators should be in the infrastructure layer")
                .WithoutRequiringPositiveResults()
                .Check(Architecture);
        }

        // [Fact]
        // public void ReadRepositories_Should_UseSpecificationEvaluator()
        // {
        //     ArchRuleDefinition
        //         .Classes()
        //         .That()
        //         .HaveNameEndingWith("ReadRepository")
        //         .Should()
        //         .DependOnAny(ArchRuleDefinition.Interfaces().That().HaveFullName("Ardalis.Specification.ISpecificationEvaluator"))
        //         .Because("read repositories should use the specification evaluator to evaluate specifications")
        //         .Check(Architecture);
        // }
    }
}
