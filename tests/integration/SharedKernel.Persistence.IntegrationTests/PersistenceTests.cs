using Ardalis.Specification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SharedKernel.Core.Domain;
using SharedKernel.Core.Events;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Persistence.Database;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace SharedKernel.Persistence.IntegrationTests;

/// <summary>Exercises real PostgreSQL isolation and transaction behavior.</summary>
public sealed partial class PersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        var context = Create(DataScope.Global);
        await using var disposecontext = context.ConfigureAwait(true);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _postgres.DisposeAsync();

    /// <summary>Missing scope is never interpreted as global permission.</summary>
    [Fact]
    public void MissingScopeFailsAndIdentityCannotChange()
    {
        var operation = new OperationContext();
        _ = Should.Throw<InvalidOperationException>(() => operation.Scope).ShouldBeOfType<InvalidOperationException>();
        operation.Initialize(DataScope.Global);
        _ = Should.Throw<InvalidOperationException>(() => operation.Initialize(DataScope.ForTenant("a"))).ShouldBeOfType<InvalidOperationException>();
    }

    /// <summary>Global data works independently; tenant rows remain isolated.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task MixedModelSeparatesGlobalAndTenantReads()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using (var global = Create(DataScope.Global))
        {
            _ = global.Add(new Country { Id = 1, Name = "Denmark" });
            _ = await global.SaveChangesAsync(cancellation).ConfigureAwait(true);
            _ = (await global.Set<Country>().ToListAsync(cancellation).ConfigureAwait(true)).ShouldHaveSingleItem();
            var forbidden = new GenericReadRepository<Order, Guid, TestDbContext>(global);
            _ = (await Should.ThrowAsync<InvalidOperationException>(() => forbidden.GetAllAsync(cancellation)).ConfigureAwait(true)).ShouldBeOfType<InvalidOperationException>();
        }

        var alpha = new Order("alpha");
        var beta = new Order("beta");
        await SeedAsync(alpha).ConfigureAwait(true);
        await SeedAsync(beta).ConfigureAwait(true);
        var read = Create(DataScope.ForTenant("alpha"), readOnly: true);
        await using var disposeread = read.ConfigureAwait(true);
        var repository = new GenericReadRepository<Order, Guid, TestDbContext>(read);
        (await repository.GetAllAsync(cancellation).ConfigureAwait(true)).ShouldHaveSingleItem().Id.ShouldBe(alpha.Id);
        (await repository.FindByIdAsync(beta.Id, cancellation).ConfigureAwait(true)).ShouldBeNull();
        read.ChangeTracker.Entries().ShouldBeEmpty();
        _ = (await read.Set<Country>().ToListAsync(cancellation).ConfigureAwait(true)).ShouldHaveSingleItem();
        _ = (await Should.ThrowAsync<InvalidOperationException>(() => read.SaveChangesAsync(cancellation)).ConfigureAwait(true)).ShouldBeOfType<InvalidOperationException>();
    }

    /// <summary>Detached updates and hard deletes cannot target another tenant's row.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task CrossTenantWritesAndFilterBypassAreRejected()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var beta = new Order("beta");
        await SeedAsync(beta).ConfigureAwait(true);
        await using (var alpha = Create(DataScope.ForTenant("alpha")))
        {
            var repository = new GenericWriteRepository<Order, Guid, TestDbContext>(alpha);
            repository.Update(beta);
            _ = await Should.ThrowAsync<Exception>(() => alpha.SaveChangesAsync(cancellation)).ConfigureAwait(true);
        }

        await using (var alpha = Create(DataScope.ForTenant("alpha")))
        {
            var forged = new Order("alpha", beta.Id) { Description = "forged" };
            _ = alpha.Update(forged);
            _ = (await Should.ThrowAsync<DbUpdateConcurrencyException>(() => alpha.SaveChangesAsync(cancellation)).ConfigureAwait(true)).ShouldBeOfType<DbUpdateConcurrencyException>();
        }

        await using (var alpha = Create(DataScope.ForTenant("alpha")))
        {
            var repository = new GenericWriteRepository<Order, Guid, TestDbContext>(alpha);
            await repository.ExecuteHardDeleteAsync([beta.Id], cancellation).ConfigureAwait(true);
            alpha.ChangeTracker.Entries().ShouldBeEmpty();
            _ = (await Should.ThrowAsync<InvalidOperationException>(() => repository.ListAsync(new BypassSpecification(), cancellation)).ConfigureAwait(true)).ShouldBeOfType<InvalidOperationException>();
        }

        var read = Create(DataScope.ForTenant("beta"));

        await using var disposeread = read.ConfigureAwait(true);
        (await read.Set<Order>().SingleAsync(cancellation).ConfigureAwait(true)).Description.ShouldBe(string.Empty);
    }

    /// <summary>Deletes are audited and soft by default; explicit hard deletion retains tenant scope.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task SoftDeleteAndAuditUseOperationIdentity()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var order = new Order("alpha");
        var context = Create(DataScope.ForTenant("alpha"));
        await using var disposecontext = context.ConfigureAwait(true);
        var repository = new GenericWriteRepository<Order, Guid, TestDbContext>(context);
        await repository.AddAsync(order, cancellation).ConfigureAwait(true);
        _ = await context.SaveChangesAsync(cancellation).ConfigureAwait(true);
        order.CreatedBy.ShouldBe("test-actor");
        repository.Delete(order);
        _ = await context.SaveChangesAsync(cancellation).ConfigureAwait(true);
        (await repository.GetAllAsync(cancellation).ConfigureAwait(true)).ShouldBeEmpty();
        order.IsDeleted.ShouldBeTrue();
        order.DeletedBy.ShouldBe("test-actor");
        _ = order.DeletedOn.ShouldNotBeNull();
        await repository.ExecuteHardDeleteAsync([order.Id], cancellation).ConfigureAwait(true);
        _ = await context.SaveChangesAsync(cancellation).ConfigureAwait(true);
        (await context.Set<Order>().IgnoreQueryFilters(["SoftDelete"]).ToListAsync(cancellation).ConfigureAwait(true)).ShouldBeEmpty();
    }

    /// <summary>The unit of work shares the repository context and does not dispose it.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task RollbackAndCommitUseTheInjectedContext()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var context = Create(DataScope.Global);
        await using var disposecontext = context.ConfigureAwait(true);
        await using (var unit = new UnitOfWork<TestDbContext>(context))
        {
            _ = await unit.BeginTransactionAsync(cancellationToken: cancellation).ConfigureAwait(true);
            _ = context.Add(new Country { Id = 1, Name = "rolled back" });
            _ = await unit.SaveChangesAsync(cancellation).ConfigureAwait(true);
            await unit.RollbackTransactionAsync(cancellation).ConfigureAwait(true);
            (await context.Set<Country>().ToListAsync(cancellation).ConfigureAwait(true)).ShouldBeEmpty();
            _ = await unit.BeginTransactionAsync(cancellationToken: cancellation).ConfigureAwait(true);
            _ = context.Add(new Country { Id = 2, Name = "committed" });
            _ = await unit.SaveChangesAsync(cancellation).ConfigureAwait(true);
            await unit.CommitTransactionAsync(cancellation).ConfigureAwait(true);
        }

        (await context.Set<Country>().SingleAsync(cancellation).ConfigureAwait(true)).Name.ShouldBe("committed");
    }

    /// <summary>Read credentials reject SQL writes independently of EF tracking settings.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task ReadPrincipalCannotWriteUsingRawSql()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var password = Guid.NewGuid().ToString("N");
        var admin = new NpgsqlConnection(_postgres.GetConnectionString());
        await using var disposeadmin = admin.ConfigureAwait(true);
        await admin.OpenAsync(cancellation).ConfigureAwait(true);
        await using (var command = admin.CreateCommand())
        {
            // Generated test-only credential; no untrusted SQL identifiers or input.
#pragma warning disable CA2100 // Password is a generated GUID; PostgreSQL DDL does not bind password parameters.
            command.CommandText = $"CREATE ROLE test_reader LOGIN PASSWORD '{password}'; GRANT CONNECT ON DATABASE postgres TO test_reader; GRANT USAGE ON SCHEMA public TO test_reader; GRANT SELECT ON ALL TABLES IN SCHEMA public TO test_reader;";
#pragma warning restore CA2100
            _ = await command.ExecuteNonQueryAsync(cancellation).ConfigureAwait(true);
        }

        var connection = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Username = "test_reader", Password = password };
        var reader = new NpgsqlConnection(connection.ConnectionString);
        await using var disposereader = reader.ConfigureAwait(true);
        await reader.OpenAsync(cancellation).ConfigureAwait(true);
        var write = reader.CreateCommand();
        await using var disposewrite = write.ConfigureAwait(true);
        write.CommandText = """INSERT INTO "Countries" ("Id", "Name") VALUES (1, 'denied')""";
        var error = (await Should.ThrowAsync<PostgresException>(() => write.ExecuteNonQueryAsync(cancellation)).ConfigureAwait(true)).ShouldBeOfType<PostgresException>();
        error.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    /// <summary>Even a warmed query plan cannot execute without an explicit operation scope.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task CachedGlobalQueryStillRequiresScope()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var initialized = Create(DataScope.Global);
        (await initialized.Set<Country>().CountAsync(cancellation).ConfigureAwait(true)).ShouldBe(0);
        var options = new DbContextOptionsBuilder<TestDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var missing = new TestDbContext(options, new OperationContext(), readOnly: false);
        _ = (await Should.ThrowAsync<InvalidOperationException>(() => missing.Set<Country>().CountAsync(cancellation)).ConfigureAwait(true)).ShouldBeOfType<InvalidOperationException>();
    }

    /// <summary>Projection specifications apply pagination once and retain untracked access.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task ProjectionPaginationIsAppliedOnce()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var context = Create(DataScope.Global);
        context.AddRange(new Country { Id = 1, Name = "first" }, new Country { Id = 2, Name = "second" }, new Country { Id = 3, Name = "third" });
        _ = await context.SaveChangesAsync(cancellation).ConfigureAwait(true);
        context.ChangeTracker.Clear();
        var repository = new GenericReadRepository<Country, int, TestDbContext>(context);
        (await repository.ListAsync(new CountryPage(), cancellation).ConfigureAwait(true)).ShouldHaveSingleItem().ShouldBe("second");
        context.ChangeTracker.Entries().ShouldBeEmpty();
    }

    /// <summary>EF-only saving cannot silently discard domain events that require an outbox.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task EfOnlyUnitOfWorkRejectsPendingDomainEvents()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var context = Create(DataScope.ForTenant("alpha"));
        var order = new Order("alpha");
        order.AddDomainEvent(new PendingEvent());
        _ = context.Add(order);
        await using var unit = new UnitOfWork<TestDbContext>(context);
        _ = (await Should.ThrowAsync<InvalidOperationException>(() => unit.SaveChangesAsync(cancellation)).ConfigureAwait(true)).ShouldBeOfType<InvalidOperationException>();
        _ = order.DomainEvents.ShouldHaveSingleItem();
        await using var verify = Create(DataScope.ForTenant("alpha"), readOnly: true);
        (await verify.Set<Order>().ToListAsync(cancellation).ConfigureAwait(true)).ShouldBeEmpty();
    }

    /// <summary>Read and write registrations cannot accidentally share one context type.</summary>
    [Fact]
    public void RepositoryRegistrationRequiresDistinctContextTypes()
    {
        var services = new ServiceCollection();
        _ = Should.Throw<InvalidOperationException>(services.AddRepositories<Order, Guid, TestDbContext, TestDbContext>).ShouldBeOfType<InvalidOperationException>();
    }

    /// <summary>Client-side specification callbacks are rejected instead of silently ignored.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task PostProcessingIsRejected()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var context = Create(DataScope.Global);
        var repository = new GenericReadRepository<Country, int, TestDbContext>(context);
        var entities = new Specification<Country>();
        _ = entities.Query.PostProcessingAction(rows => rows.Take(1));
        var projection = new Specification<Country, string>();
        projection.Query.Select(country => country.Name);
        _ = projection.Query.PostProcessingAction(rows => rows.Take(1));
        _ = (await Should.ThrowAsync<NotSupportedException>(() => repository.ListAsync(entities, cancellation)).ConfigureAwait(true)).ShouldBeOfType<NotSupportedException>();
        _ = (await Should.ThrowAsync<NotSupportedException>(() => repository.ListAsync(projection, cancellation)).ConfigureAwait(true)).ShouldBeOfType<NotSupportedException>();
    }

    private TestDbContext Create(DataScope scope, bool readOnly = false)
    {
        var operation = new OperationContext();
        operation.Initialize(scope, "test-actor");
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new TestDbContext(options, operation, readOnly);
    }

    private async Task SeedAsync(Order order)
    {
        var context = Create(DataScope.ForTenant(order.TenantId));
        await using var disposecontext = context.ConfigureAwait(true);
        _ = context.Add(order);
        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private sealed class PendingEvent : DomainEvent;

    private sealed class CountryPage : Specification<Country, string>
    {
        public CountryPage()
        {
            _ = Query.OrderBy(country => country.Id).Skip(1).Take(1);
            Query.Select(country => country.Name);
        }
    }

    private sealed class BypassSpecification : Specification<Order>
    {
        public BypassSpecification()
        {
            _ = Query.IgnoreQueryFilters();
        }
    }

    private sealed class TestDbContext(DbContextOptions options, OperationContext operation, bool readOnly) : ScopedDbContext(options, operation, readOnly)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            _ = modelBuilder.Entity<Order>().ToTable("Orders");
            _ = modelBuilder.Entity<Order>().HasMany(order => order.Lines).WithOne().HasForeignKey("OrderId");
            _ = modelBuilder.Entity<OrderLine>().ToTable("OrderLines");
            _ = modelBuilder.Entity<Country>().ToTable("Countries");
            base.OnModelCreating(modelBuilder);
        }
    }

    private sealed class Country : IReadModel<int>
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class Order : BaseEntity<Guid>, ITenantScoped, IAggregateRoot
    {
        public Order(string tenantId, Guid? id = null)
        {
            Id = id ?? Guid.NewGuid();
            TenantId = tenantId;
        }

        private Order()
        {
        }

        public string TenantId { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public List<OrderLine> Lines { get; } = [];
    }
}
