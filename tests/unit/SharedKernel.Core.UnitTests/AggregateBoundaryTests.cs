using SharedKernel.Core.Database;
using SharedKernel.Core.Domain;
using Shouldly;
using Xunit;

namespace SharedKernel.Core.UnitTests;

/// <summary>Guards the application repository boundary between roots and child entities.</summary>
public sealed class AggregateBoundaryTests
{
    /// <summary>Child entities can be queried but cannot have independent write repositories.</summary>
    [Fact]
    public void ChildEntitiesAreReadOnlyRepositoryTargets()
    {
        var read = typeof(IGenericReadRepository<,>).MakeGenericType(typeof(Child), typeof(Guid));
        _ = read.ShouldNotBeNull();
        _ = Should.Throw<ArgumentException>(() =>
            typeof(IGenericWriteRepository<,>).MakeGenericType(typeof(Child), typeof(Guid))).ShouldBeOfType<ArgumentException>();
    }

    /// <summary>An explicitly marked aggregate root can have a write repository.</summary>
    [Fact]
    public void AggregateRootsAreWriteRepositoryTargets()
    {
        var write = typeof(IGenericWriteRepository<,>).MakeGenericType(typeof(Root), typeof(Guid));
        _ = write.ShouldNotBeNull();
    }

    private sealed class Root : BaseEntity<Guid>, IAggregateRoot;

    private sealed class Child : BaseEntity<Guid>;
}
