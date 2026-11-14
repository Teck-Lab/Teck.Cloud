using Ardalis.Specification;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Domain;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Persistence.Database;
using Shouldly;
using Xunit;

namespace SharedKernel.Persistence.IntegrationTests;

/// <summary>Specification behavior against a real relational provider.</summary>
public sealed partial class PersistenceTests
{
    /// <summary>Unique queries return null or one row and reject ambiguous matches for entities and projections.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task SingleSpecificationsRejectDuplicates()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var context = Create(DataScope.Global);
        context.AddRange(new Country { Id = 1, Name = "same" }, new Country { Id = 2, Name = "same" });
        _ = await context.SaveChangesAsync(cancellation).ConfigureAwait(true);
        context.ChangeTracker.Clear();
        var repository = new GenericReadRepository<Country, int, TestDbContext>(context);
        var all = new SingleResultSpecification<Country>();
        var names = new SingleResultSpecification<Country, string>();
        names.Query.Select(country => country.Name);
        _ = (await Should.ThrowAsync<InvalidOperationException>(() => repository.SingleOrDefaultAsync(all, cancellation)).ConfigureAwait(true)).ShouldBeOfType<InvalidOperationException>();
        _ = (await Should.ThrowAsync<InvalidOperationException>(() => repository.SingleOrDefaultAsync(names, cancellation)).ConfigureAwait(true)).ShouldBeOfType<InvalidOperationException>();
        _ = all.Query.Where(country => country.Id == 1);
        _ = names.Query.Where(country => country.Id == 1);
        ((await repository.SingleOrDefaultAsync(all, cancellation).ConfigureAwait(true))?.Id).ShouldBe(1);
        (await repository.SingleOrDefaultAsync(names, cancellation).ConfigureAwait(true)).ShouldBe("same");
        _ = all.Query.Where(country => country.Id == 99);
        _ = names.Query.Where(country => country.Id == 99);
        (await repository.SingleOrDefaultAsync(all, cancellation).ConfigureAwait(true)).ShouldBeNull();
        (await repository.SingleOrDefaultAsync(names, cancellation).ConfigureAwait(true)).ShouldBeNull();
        context.ChangeTracker.Entries().ShouldBeEmpty();
    }

    /// <summary>Paging cannot conceal duplicate matches in single-result queries.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task SingleSpecificationsRejectPaginationAndProjectionCallbacks()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var context = Create(DataScope.Global);
        var repository = new GenericReadRepository<Country, int, TestDbContext>(context);
        var entity = new SingleResultSpecification<Country>();
        _ = entity.Query.Take(1);
        _ = (await Should.ThrowAsync<InvalidOperationException>(() => repository.SingleOrDefaultAsync(entity, cancellation)).ConfigureAwait(true)).ShouldBeOfType<InvalidOperationException>();
        var projection = new SingleResultSpecification<Country, string>();
        projection.Query.Select(country => country.Name);
        _ = projection.Query.Skip(1);
        _ = (await Should.ThrowAsync<InvalidOperationException>(() => repository.SingleOrDefaultAsync(projection, cancellation)).ConfigureAwait(true)).ShouldBeOfType<InvalidOperationException>();
        var callback = new SingleResultSpecification<Country, string>();
        callback.Query.Select(country => country.Name);
        _ = callback.Query.PostProcessingAction(rows => rows.Take(1));
        _ = (await Should.ThrowAsync<NotSupportedException>(() => repository.SingleOrDefaultAsync(callback, cancellation)).ConfigureAwait(true)).ShouldBeOfType<NotSupportedException>();
        _ = (await Should.ThrowAsync<NotSupportedException>(() => repository.FirstOrDefaultAsync(callback, cancellation)).ConfigureAwait(true)).ShouldBeOfType<NotSupportedException>();
    }

    /// <summary>A specification loads a tenant-scoped aggregate graph for tracked mutation without explicit Update.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task WriteSpecificationLoadsAndSavesAggregateChildren()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var alpha = new Order("alpha");
        alpha.Lines.Add(new OrderLine("alpha", "original"));
        var deleted = new OrderLine("alpha", "hidden");
        deleted.SetDeletedProperties(isDeleted: true, "test");
        alpha.Lines.Add(deleted);
        var beta = new Order("beta");
        beta.Lines.Add(new OrderLine("beta", "other tenant"));
        await SeedAsync(alpha).ConfigureAwait(true);
        await SeedAsync(beta).ConfigureAwait(true);
        await using (var context = Create(DataScope.ForTenant("alpha")))
        {
            context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            var repository = new GenericWriteRepository<Order, Guid, TestDbContext>(context);
            (await repository.LoadForUpdateAsync(new OrderWithLines(beta.Id), cancellation).ConfigureAwait(true)).ShouldBeNull();
            var loaded = await repository.LoadForUpdateAsync(new OrderWithLines(alpha.Id), cancellation).ConfigureAwait(true);
            _ = loaded.ShouldNotBeNull();
            var line = loaded.Lines.ShouldHaveSingleItem();
            line.Description.ShouldBe("original");
            line.Description = "changed through root";
            context.Entry(line).State.ShouldBe(EntityState.Modified);
            _ = await context.SaveChangesAsync(cancellation).ConfigureAwait(true);
        }

        await using var read = Create(DataScope.ForTenant("alpha"), readOnly: true);
        var reader = new GenericReadRepository<Order, Guid, TestDbContext>(read);
        var saved = await reader.SingleOrDefaultAsync(new OrderWithLines(alpha.Id), cancellation).ConfigureAwait(true);
        _ = saved.ShouldNotBeNull();
        saved.Lines.ShouldHaveSingleItem().Description.ShouldBe("changed through root");
        read.ChangeTracker.Entries().ShouldBeEmpty();
        var forbidden = new GenericWriteRepository<Order, Guid, TestDbContext>(read);
        _ = (await Should.ThrowAsync<InvalidOperationException>(() => forbidden.LoadForUpdateAsync(new OrderWithLines(alpha.Id), cancellation)).ConfigureAwait(true)).ShouldBeOfType<InvalidOperationException>();
    }

    /// <summary>Write specifications cannot bypass filters, disable tracking, paginate or silently post-process.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task WriteSpecificationsPreserveSafetyAndUniqueness()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await SeedAsync(new Order("alpha")).ConfigureAwait(true);
        await SeedAsync(new Order("alpha")).ConfigureAwait(true);
        await using var context = Create(DataScope.ForTenant("alpha"));
        var repository = new GenericWriteRepository<Order, Guid, TestDbContext>(context);
        _ = (await Should.ThrowAsync<InvalidOperationException>(() => repository.LoadForUpdateAsync(new SingleResultSpecification<Order>(), cancellation)).ConfigureAwait(true)).ShouldBeOfType<InvalidOperationException>();
        var bypass = new SingleResultSpecification<Order>();
        _ = bypass.Query.IgnoreQueryFilters();
        var untracked = new SingleResultSpecification<Order>();
        _ = untracked.Query.AsNoTracking();
        var identity = new SingleResultSpecification<Order>();
        _ = identity.Query.AsNoTrackingWithIdentityResolution();
        var paged = new SingleResultSpecification<Order>();
        _ = paged.Query.Take(1);
        foreach (var specification in new[] { bypass, untracked, identity, paged })
        {
            _ = (await Should.ThrowAsync<InvalidOperationException>(() => repository.LoadForUpdateAsync(specification, cancellation)).ConfigureAwait(true)).ShouldBeOfType<InvalidOperationException>();
        }

        var callback = new SingleResultSpecification<Order>();
        _ = callback.Query.PostProcessingAction(rows => rows.Take(1));
        _ = (await Should.ThrowAsync<NotSupportedException>(() => repository.LoadForUpdateAsync(callback, cancellation)).ConfigureAwait(true)).ShouldBeOfType<NotSupportedException>();
    }

    /// <summary>Projection reuse keeps SQL filtering/paging while criteria-only counts remain independent.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task ReusedProjectionKeepsPagingAndTenantFilters()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await SeedAsync(new Order("alpha") { Description = "a" }).ConfigureAwait(true);
        await SeedAsync(new Order("alpha") { Description = "b" }).ConfigureAwait(true);
        await SeedAsync(new Order("beta") { Description = "c" }).ConfigureAwait(true);
        await using var context = Create(DataScope.ForTenant("alpha"), readOnly: true);
        var repository = new GenericReadRepository<Order, Guid, TestDbContext>(context);
        var filter = new Specification<Order>();
        _ = filter.Query.OrderBy(order => order.Description).Skip(1).Take(1);
        var shape = new Specification<Order, string>();
        shape.Query.Select(order => order.Description);
        var combined = filter.WithProjectionOf(shape);
        (await repository.ListAsync(combined, cancellation).ConfigureAwait(true)).ShouldHaveSingleItem().ShouldBe("b");
        (await repository.CountAsync(combined, cancellation).ConfigureAwait(true)).ShouldBe(2);
        var emptyPage = new Specification<Order>();
        _ = emptyPage.Query.OrderBy(order => order.Id).Skip(50).Take(1);
        (await repository.AnyAsync(emptyPage, cancellation).ConfigureAwait(true)).ShouldBeTrue();
        (await repository.ListAsync(emptyPage, cancellation).ConfigureAwait(true)).ShouldBeEmpty();
        shape.Skip.ShouldBe(-1);
        shape.Take.ShouldBe(-1);
        context.ChangeTracker.Entries().ShouldBeEmpty();
    }

    /// <summary>Identity resolution deduplicates read results without attaching entities to the context.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task ReadIdentityResolutionRemainsUntracked()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var order = new Order("alpha");
        order.Lines.Add(new OrderLine("alpha", "one"));
        order.Lines.Add(new OrderLine("alpha", "two"));
        await SeedAsync(order).ConfigureAwait(true);
        await using var context = Create(DataScope.ForTenant("alpha"));
        var repository = new GenericReadRepository<Order, Guid, TestDbContext>(context);
        var specification = new Specification<Order, Order>();
        specification.Query.SelectMany(root => root.Lines.Select(_ => root));
        _ = specification.Query.AsNoTrackingWithIdentityResolution();
        var rows = await repository.ListAsync(specification, cancellation).ConfigureAwait(true);
        rows.Count.ShouldBe(2);
        rows[1].ShouldBeSameAs(rows[0]);
        context.ChangeTracker.Entries().ShouldBeEmpty();
        var tracked = new SingleResultSpecification<Order>();
        _ = tracked.Query.AsTracking();
        _ = (await Should.ThrowAsync<InvalidOperationException>(() => repository.SingleOrDefaultAsync(tracked, cancellation)).ConfigureAwait(true)).ShouldBeOfType<InvalidOperationException>();
        var bypass = new SingleResultSpecification<Order, string>();
        bypass.Query.Select(root => root.Description);
        _ = bypass.Query.IgnoreQueryFilters();
        _ = (await Should.ThrowAsync<InvalidOperationException>(() => repository.SingleOrDefaultAsync(bypass, cancellation)).ConfigureAwait(true)).ShouldBeOfType<InvalidOperationException>();
    }

    private sealed class OrderWithLines : SingleResultSpecification<Order>
    {
        public OrderWithLines(Guid id)
        {
            _ = Query.Where(order => order.Id == id).Include(order => order.Lines).AsSplitQuery();
        }
    }

    private sealed class OrderLine : BaseEntity<Guid>, ITenantScoped
    {
        public OrderLine(string tenantId, string description)
        {
            Id = Guid.NewGuid();
            TenantId = tenantId;
            Description = description;
        }

        private OrderLine()
        {
        }

        public string TenantId { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
    }
}
