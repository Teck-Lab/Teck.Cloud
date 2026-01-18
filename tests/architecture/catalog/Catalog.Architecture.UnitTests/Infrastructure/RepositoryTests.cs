
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using Ardalis.Specification;
using SharedKernel.Core.Database;
using SharedKernel.Persistence.Database.EFCore;

namespace Catalog.Arch.UnitTests.Infrastructure
{
    public class RepositoryTests : ArchUnitBaseTest
    {
        [Fact]
        public void WriteRepositories_Should_ImplementCorrectInterface()
        {
            var rule = ArchRuleDefinition
                .Classes()
                .That()
                .HaveNameEndingWith("WriteRepository")
                .Should()
                .BeAssignableTo(typeof(GenericWriteRepository<,,>))
                .AndShould()
                .ImplementInterface(typeof(IGenericWriteRepository<,>))
                .Because("write repositories should inherit from GenericWriteRepository and implement their specific interface");

            rule.Check(Architecture);
        }

        [Fact]
        public void ReadRepositories_Should_ImplementCorrectInterface()
        {
            var rule = ArchRuleDefinition
                .Classes()
                .That()
                .HaveNameEndingWith("ReadRepository")
                .Should()
                .BeAssignableTo(typeof(GenericReadRepository<,,>))
                .AndShould()
                .ImplementInterface(typeof(IGenericReadRepository<,>))
                .Because("read repositories should inherit from GenericReadRepository and implement their specific interface");

            rule.Check(Architecture);
        }

        [Fact]
        public void Repositories_Should_BeSealed()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .HaveNameEndingWith("Repository")
                .Should()
                .BeSealed()
                .Because("repositories should be sealed to prevent inheritance")
                .Check(Architecture);
        }

        [Fact]
        public void ReadRepositories_Should_NotDependOnWriteDbContext()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .HaveNameEndingWith("ReadRepository")
                .Should()
                .NotDependOnAny(ArchRuleDefinition.Classes().That().HaveNameEndingWith("WriteDbContext"))
                .Because("read repositories should only depend on read contexts")
                .Check(Architecture);
        }

        [Fact]
        public void WriteRepositories_Should_NotDependOnReadDbContext()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .HaveNameEndingWith("WriteRepository")
                .Should()
                .NotDependOnAny(ArchRuleDefinition.Classes().That().HaveNameEndingWith("ReadDbContext"))
                .Because("write repositories should only depend on write contexts")
                .Check(Architecture);
        }

        [Fact]
        public void ReadRepositories_Should_BeInReadRepositoriesNamespace()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .HaveNameEndingWith("ReadRepository")
                .Should()
                .ResideInNamespaceMatching(@"^.*\.Repositories\.Read$")
                .Because("read repositories should be organized in a Read namespace")
                .Check(Architecture);
        }

        [Fact]
        public void WriteRepositories_Should_BeInWriteRepositoriesNamespace()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .HaveNameEndingWith("WriteRepository")
                .Should()
                .ResideInNamespaceMatching(@"^.*\.Repositories\.Write$")
                .Because("write repositories should be organized in a Write namespace")
                .Check(Architecture);
        }

        [Fact]
        public void DomainSpecifications_Should_BeInDomainLayer()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .ImplementInterface(typeof(ISpecification<>))
                .And()
                .ResideInNamespaceMatching(@"^.*\.Domain\..*")
                .Should()
                .ResideInNamespaceMatching(@"^.*\.Specifications$")
                .Because("domain specifications should be in the domain layer in a Specifications namespace")
                .Check(Architecture);
        }

        [Fact]
        public void ApplicationSpecifications_Should_BeInApplicationLayer()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .ImplementInterface(typeof(ISpecification<>))
                .And()
                .ResideInNamespaceMatching(@"^.*\.Application\..*")
                .Should()
                .ResideInNamespaceMatching(@"^.*\.Specifications$")
                .Because("application specifications should be in the application layer in a Specifications namespace")
                .Check(Architecture);
        }

        // [Fact]
        // public void ReadRepositories_Should_DependOnSpecificationInterface()
        // {
        //     ArchRuleDefinition
        //         .Classes()
        //         .That()
        //         .HaveNameEndingWith("ReadRepository")
        //         .Should()
        //         .DependOnAny(ArchRuleDefinition.Interfaces().That().ImplementInterface(typeof(ISpecification<>)))
        //         .Because("read repositories should use the specification pattern")
        //         .Check(Architecture);
        // }

        [Fact]
        public void WriteRepositories_Should_NotUseAppDbContext()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .HaveNameEndingWith("WriteRepository")
                .Should()
                .NotDependOnAny(ArchRuleDefinition.Classes().That().HaveFullNameContaining("AppDbContext"))
                .Because("write repositories should use ApplicationWriteDbContext instead of the legacy AppDbContext")
                .Check(Architecture);
        }

        [Fact]
        public void ReadRepositories_Should_NotUseAppDbContext()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .HaveNameEndingWith("ReadRepository")
                .Should()
                .NotDependOnAny(ArchRuleDefinition.Classes().That().HaveFullNameContaining("AppDbContext"))
                .Because("read repositories should use ApplicationReadDbContext instead of the legacy AppDbContext")
                .Check(Architecture);
        }

        [Fact]
        public void WriteRepositories_Should_UseApplicationWriteDbContext()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .HaveNameEndingWith("WriteRepository")
                .Should()
                .DependOnAny(ArchRuleDefinition.Classes().That().HaveFullNameContaining("ApplicationWriteDbContext"))
                .Because("write repositories should use ApplicationWriteDbContext")
                .Check(Architecture);
        }

        [Fact]
        public void ReadRepositories_Should_UseApplicationReadDbContext()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .HaveNameEndingWith("ReadRepository")
                .Should()
                .DependOnAny(ArchRuleDefinition.Classes().That().HaveFullNameContaining("ApplicationReadDbContext"))
                .Because("read repositories should use ApplicationReadDbContext")
                .Check(Architecture);
        }
    }
}
