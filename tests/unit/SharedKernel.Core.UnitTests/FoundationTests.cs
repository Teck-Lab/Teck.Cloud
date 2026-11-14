using System.Data.Common;
using AutoFixture;
using SharedKernel.Core.Database;
using SharedKernel.Core.Domain;
using SharedKernel.Core.Events;
using SharedKernel.Core.Exceptions;
using SharedKernel.Core.Pagination;
using Shouldly;
using Soenneker.Utils.AutoBogus;
using Xunit;

namespace SharedKernel.Core.UnitTests;

/// <summary>Regression tests for the imported foundation.</summary>
public sealed class FoundationTests
{
    /// <summary>Unequal component lengths must never compare equal in either direction.</summary>
    [Fact]
    public void ValueEqualityRejectsUnequalLengths()
    {
        var shortValue = new Components(1);
        var longValue = new Components(1, 2);
        longValue.Equals(shortValue).ShouldBeFalse();
        shortValue.Equals(longValue).ShouldBeFalse();
        new Components(1).Equals(new Components()).ShouldBeFalse();
    }

    /// <summary>Equal values have equal hashes, including null components.</summary>
    [Fact]
    public void ValueEqualityPreservesEqualityAndHash()
    {
        var first = new Components(1, null, "value");
        var second = new Components(1, null, "value");
        first.Equals(second).ShouldBeTrue();
        second.GetHashCode().ShouldBe(first.GetHashCode());
        first.Equals(new Components(1, 2, "value")).ShouldBeFalse();
        first.Equals(null).ShouldBeFalse();
    }

    /// <summary>Events are ordered, protected from external mutation and drained once.</summary>
    [Fact]
    public void EntityDrainsEventsInOrder()
    {
        var entity = new Entity();
        var first = new Changed();
        var second = new Changed();
        entity.AddDomainEvent(first);
        entity.AddDomainEvent(second);
        var snapshot = entity.DomainEvents;
        entity.ClearDomainEvents().ShouldBe([first, second]);
        entity.DomainEvents.ShouldBeEmpty();
        entity.ClearDomainEvents().ShouldBeEmpty();
        snapshot.Count.ShouldBe(2);
        entity.Id.ShouldNotBe(Guid.Empty);
    }

    /// <summary>Null events cannot poison the event queue.</summary>
    [Fact]
    public void EntityRejectsNullEvents() =>
        Should.Throw<ArgumentNullException>(() => new Entity().AddDomainEvent(null!)).ShouldBeOfType<ArgumentNullException>();

    /// <summary>Page sizes must be positive.</summary>
    /// <param name="size">The invalid size.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PaginationRejectsInvalidSize(int size) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new Paging { Size = size }).ShouldBeOfType<ArgumentOutOfRangeException>();

    /// <summary>Page numbers must be positive.</summary>
    /// <param name="page">The invalid page.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PaginationRejectsInvalidPage(int page) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new Paging { Page = page }).ShouldBeOfType<ArgumentOutOfRangeException>();

    /// <summary>The existing upper page size limit is retained.</summary>
    [Fact]
    public void PaginationCapsSize() => new Paging { Size = 101 }.Size.ShouldBe(100);

    /// <summary>A page cannot divide by zero or carry negative totals.</summary>
    [Fact]
    public void PagedListRejectsInvalidInputs()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new PagedList<int>([], 1, 1, 0)).ShouldBeOfType<ArgumentOutOfRangeException>();
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new PagedList<int>([], -1, 1, 10)).ShouldBeOfType<ArgumentOutOfRangeException>();
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new PagedList<int>([], 1, 0, 10)).ShouldBeOfType<ArgumentOutOfRangeException>();
    }

    /// <summary>Page counts round up and handle empty results.</summary>
    [Fact]
    public void PagedListCountsPages()
    {
        new PagedList<int>([21], 21, 3, 10).TotalPages.ShouldBe(3);
        new PagedList<int>([21], 21, 3, 10).IsLastPage.ShouldBeTrue();
        new PagedList<int>([], 0, 1, 10).TotalPages.ShouldBe(0);
    }

    /// <summary>Special characters in credentials cannot inject connection parameters.</summary>
    [Fact]
    public void ConnectionStringsEscapeCredentials()
    {
        const string password = "secret;Host=attacker";
        var credentials = new Fixture().Build<DatabaseCredentials>().Without(value => value.AdditionalParameters).Create() with
        {
            Host = "database.internal",
            Application = new UserCredentials { Username = "application", Password = password },
        };
        var parsed = new DbConnectionStringBuilder { ConnectionString = credentials.GetApplicationConnectionString() };
        parsed["Password"].ShouldBe(password);
        parsed["Host"].ShouldBe("database.internal");
    }

    /// <summary>Diagnostic record formatting must not print secrets.</summary>
    [Fact]
    public void CredentialsRedactDiagnosticStrings()
    {
        var credentials = new AutoFaker<DatabaseCredentials>().UseSeed(873).Generate();
        credentials.ToString().ShouldNotContain(credentials.Application.Password);
        credentials.ToString().ShouldNotContain(credentials.Admin.Password);
        credentials.Application.ToString().ShouldNotContain(credentials.Application.Password);
    }

    /// <summary>Configuration failures preserve the original cause.</summary>
    [Fact]
    public void ConfigurationExceptionPreservesCause()
    {
        var cause = new IOException("test failure");
        new ConfigurationMissingException("Missing section", cause).InnerException.ShouldBeSameAs(cause);
    }

    private sealed class Components(params object?[] values) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents() => values;
    }

    private sealed class Entity : BaseEntity;

    private sealed class Changed : DomainEvent;

    private sealed class Paging : PaginationParameters;
}
