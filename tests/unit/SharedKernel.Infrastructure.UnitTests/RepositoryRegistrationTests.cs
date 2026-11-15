using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Core.Database;
using SharedKernel.Core.Domain;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Persistence.Database;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Xunit;

namespace SharedKernel.Infrastructure.UnitTests;

/// <summary>Proves independent repository registration for distinct read and write models.</summary>
public sealed class RepositoryRegistrationTests
{
    /// <summary>Read projections do not force the write aggregate into the read context registration.</summary>
    [Fact]
    public void DifferentReadAndWriteModelsHaveDifferentRepositories()
    {
        var services = new ServiceCollection();
        _ = services.AddReadRepository<OrderReadModel, Guid, ReadContext>();
        _ = services.AddWriteRepository<Order, Guid, WriteContext>();

        Assert.Contains(services, registration =>
            registration.ServiceType == typeof(IGenericReadRepository<OrderReadModel, Guid>)
            && registration.ImplementationType == typeof(GenericReadRepository<OrderReadModel, Guid, ReadContext>));
        Assert.Contains(services, registration =>
            registration.ServiceType == typeof(IGenericWriteRepository<Order, Guid>)
            && registration.ImplementationType == typeof(GenericWriteRepository<Order, Guid, WriteContext>));
        Assert.DoesNotContain(services, registration => registration.ServiceType == typeof(IGenericReadRepository<Order, Guid>));
    }

    internal sealed class Order : IAggregateRoot, IReadModel<Guid>
    {
        public Guid Id { get; set; }
    }

    internal sealed class OrderReadModel : IReadModel<Guid>
    {
        public Guid Id { get; set; }
    }

    internal sealed class ReadContext(DbContextOptions options, OperationContext operation)
        : ScopedDbContext(options, operation, readOnly: true);

    internal sealed class WriteContext(DbContextOptions options, OperationContext operation, IDbContextOutbox outbox, IMessageContext messaging)
        : WriteDbContext(options, operation, outbox, messaging);
}
