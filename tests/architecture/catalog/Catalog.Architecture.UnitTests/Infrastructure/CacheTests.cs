using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;

namespace Catalog.Arch.UnitTests.Infrastructure
{
    public class CacheTests : ArchUnitBaseTest
    {
        [Fact]
        public void Caches_Should_BeInInfrastructureLayer()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .HaveNameEndingWith("Cache")
                .Should()
                .ResideInNamespaceMatching(@"^.*\.Infrastructure\..*")
                .Because("cache implementations should be in the infrastructure layer")
                .Check(Architecture);
        }

        [Fact]
        public void CacheInterfaces_Should_BeInApplicationLayer()
        {
            ArchRuleDefinition
                .Interfaces()
                .That()
                .HaveNameEndingWith("Cache")
                .Should()
                .ResideInNamespaceMatching(@"^.*\.Application\..*")
                .Because("cache interfaces should be in the application layer")
                .Check(Architecture);
        }

        [Fact]
        public void Caches_Should_BeSealed()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .HaveNameEndingWith("Cache")
                .And()
                .DoNotHaveNameEndingWith("CacheBase")
                .Should()
                .BeSealed()
                .Because("caches should be sealed to prevent inheritance")
                .Check(Architecture);
        }

        [Fact]
        public void Caches_Should_DependOnReadModels()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .HaveNameEndingWith("Cache")
                .Should()
                .DependOnAny(ArchRuleDefinition.Classes().That().ResideInNamespaceMatching(@"^.*\.ReadModels$"))
                .Because("caches should store read models, not domain entities")
                .Check(Architecture);
        }

        [Fact]
        public void Caches_Should_NotDependOnDomainEntities()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .HaveNameEndingWith("Cache")
                .Should()
                .NotDependOnAny(ArchRuleDefinition.Classes().That().ResideInNamespaceMatching(@"^.*\.Domain\.Entities(?:\..*)?$"))
                .Because("caches should store read models, not domain entities")
                .Check(Architecture);
        }

        [Fact]
        public void CacheInterfaces_Should_SpecifyReadModels()
        {
            ArchRuleDefinition
                .Interfaces()
                .That()
                .HaveNameEndingWith("Cache")
                .Should()
                .DependOnAny(ArchRuleDefinition.Classes().That().ResideInNamespaceMatching(@"^.*\.ReadModels$"))
                .OrShould()
                .ImplementInterface(typeof(SharedKernel.Core.Caching.IGenericCacheService<,>))
                .Because("cache interfaces should specify read models as their cached type or implement IGenericCacheService<,>")
                .Check(Architecture);
        }
    }
}
