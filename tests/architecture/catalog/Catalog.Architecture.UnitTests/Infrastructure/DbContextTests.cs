using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using SharedKernel.Persistence.Database.EFCore;

namespace Catalog.Arch.UnitTests.Infrastructure
{
    public class DbContextTests : ArchUnitBaseTest
    {
        [Fact]
        public void ReadDbContext_Should_InheritFromBaseDbContext()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .HaveNameEndingWith("ReadDbContext")
                .Should()
                .BeAssignableTo(typeof(BaseDbContext))
                .Because("read db contexts should inherit from BaseDbContext")
                .Check(Architecture);
        }

        [Fact]
        public void WriteDbContext_Should_InheritFromBaseDbContext()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .HaveNameEndingWith("WriteDbContext")
                .Should()
                .BeAssignableTo(typeof(BaseDbContext))
                .Because("write db contexts should inherit from BaseDbContext")
                .Check(Architecture);
        }

        [Fact]
        public void ReadDbContext_Should_BeSealed()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .HaveNameEndingWith("ReadDbContext")
                .Should()
                .BeSealed()
                .Because("db contexts should be sealed to prevent inheritance")
                .Check(Architecture);
        }

        // [Fact]
        // public void WriteDbContext_Should_BeSealed()
        // {
        //     ArchRuleDefinition
        //         .Classes()
        //         .That()
        //         .HaveNameEndingWith("WriteDbContext")
        //         .Should()
        //         .BeSealed()
        //         .Because("db contexts should be sealed to prevent inheritance")
        //         .Check(Architecture);
        // }

        [Fact]
        public void ReadDbContext_Should_NotDependOnDomainEntities()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .HaveNameEndingWith("ReadDbContext")
                .Should()
                .NotDependOnAny(ArchRuleDefinition.Classes().That().ResideInNamespaceMatching(@"^.*\.Domain\.Entities(?:\..*)?$"))
                .Because("read db contexts should depend on read models, not domain entities")
                .Check(Architecture);
        }

        [Fact]
        public void WriteDbContext_Should_NotDependOnReadModels()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .HaveNameEndingWith("WriteDbContext")
                .Should()
                .NotDependOnAny(ArchRuleDefinition.Classes().That().ResideInNamespaceMatching(@"^.*\.ReadModels$"))
                .Because("write db contexts should depend on domain entities, not read models")
                .Check(Architecture);
        }

        [Fact]
        public void ApplicationReadDbContext_Should_DependOnReadModels()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .HaveFullName("Catalog.Infrastructure.Persistence.ApplicationReadDbContext")
                .Should()
                .DependOnAny(ArchRuleDefinition.Classes().That().ResideInNamespaceMatching(@"^.*\.ReadModels$"))
                .Because("ApplicationReadDbContext should reference read models")
                .Check(Architecture);
        }

        [Fact]
        public void ApplicationWriteDbContext_Should_DependOnDomainEntities()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .HaveFullName("Catalog.Infrastructure.Persistence.ApplicationWriteDbContext")
                .Should()
                .DependOnAny(ArchRuleDefinition.Classes().That().ResideInNamespaceMatching(@"^.*\.Domain\.Entities(?:\..*)?$"))
                .Because("ApplicationWriteDbContext should reference domain entities")
                .Check(Architecture);
        }
    }
}
