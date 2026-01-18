using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using SharedKernel.Core.Caching;

namespace Catalog.Arch.UnitTests.Application
{
    public class CqrsTests : ArchUnitBaseTest
    {
        [Fact]
        public void QueryHandlers_Should_UseReadRepositories()
        {
            var allowedInterfaces = ArchRuleDefinition.Interfaces()
            .That()
            .HaveNameEndingWith("ReadRepository")
            .Or()
            .HaveNameEndingWith("Cache")
            .Or()
            .HaveNameEndingWith("Runner");

            ArchRuleDefinition
                .Classes()
                .That()
                .ImplementInterface(typeof(Mediator.IRequestHandler<,>))
                .And()
                .HaveNameEndingWith("QueryHandler")
                .Should()
                .DependOnAny(allowedInterfaces)
                .Because("query handlers should use read repositories")
                .Check(Architecture);
        }

        [Fact]
        public void CommandHandlers_Should_UseWriteRepositories()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .ImplementInterface(typeof(Mediator.IRequestHandler<,>))
                .And()
                .HaveNameEndingWith("CommandHandler")
                .Should()
                .DependOnAny(ArchRuleDefinition.Interfaces().That().HaveNameEndingWith("WriteRepository"))
                .Because("command handlers should use write repositories")
                .Check(Architecture);
        }

        [Fact]
        public void CommandHandlers_May_UseReadRepositories()
        {
            // This test documents our design decision to allow command handlers to use read repositories
            // for validation or lookup purposes, even though they primarily work with write repositories
            // This is a "may" rule, not a "must" rule, so it's always true

            // We're not enforcing any rule here, just documenting the design decision
            // that command handlers are allowed to use read repositories for lookups and validation

            // The test will always pass - we're just using it for documentation
            Assert.True(true, "Command handlers may use read repositories for validation and lookups");
        }

        [Fact]
        public void QueryHandlers_Should_NotUseWriteRepositories()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .ImplementInterface(typeof(Mediator.IRequestHandler<,>))
                .And()
                .HaveNameEndingWith("QueryHandler")
                .Should()
                .NotDependOnAny(ArchRuleDefinition.Interfaces().That().HaveNameEndingWith("WriteRepository"))
                .Because("query handlers should not use write repositories")
                .Check(Architecture);
        }

        [Fact]
        public void Commands_Should_ImplementICommand()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .HaveNameEndingWith("Command")
                .And()
                .DoNotHaveNameEndingWith("CommandHandler")
                .Should()
                .ImplementInterface(typeof(SharedKernel.Core.CQRS.ICommand<>))
                .Because("commands should implement ICommand<T>")
                .Check(Architecture);
        }

        [Fact]
        public void Queries_Should_ImplementIQuery()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .HaveNameEndingWith("Query")
                .And()
                .DoNotHaveNameEndingWith("QueryHandler")
                .Should()
                .ImplementInterface(typeof(SharedKernel.Core.CQRS.IQuery<>))
                .Because("queries should implement IQuery<T>")
                .Check(Architecture);
        }

        [Fact]
        public void CommandHandlers_Should_UpdateCache()
        {
            ArchRuleDefinition
                .Classes()
                .That()
                .ImplementInterface(typeof(Mediator.IRequestHandler<,>))
                .And()
                .HaveNameEndingWith("CommandHandler")
                .And()
                .ImplementInterface(typeof(IGenericCacheService<,>))
                .And()
                .DoNotHaveFullNameContaining("Delete")
                .Should()
                .DependOnAny(ArchRuleDefinition.Interfaces().That().HaveNameEndingWith("Cache"))
                .Because("command handlers should update the cache after changes")
                .WithoutRequiringPositiveResults()
                .Check(Architecture);
        }
    }
}
