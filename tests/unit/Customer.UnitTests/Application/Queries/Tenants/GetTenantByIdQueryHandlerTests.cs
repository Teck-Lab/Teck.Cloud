using Customer.Application.Tenants.Features.GetTenantById.V1;
using Customer.Application.Tenants.Repositories;
using Customer.Application.Tenants.ReadModels;
using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Pricing;
using Shouldly;

namespace Customer.UnitTests.Application.Queries.Tenants;

public sealed class GetTenantByIdQueryHandlerTests
{
    private readonly ITenantReadRepository _tenantReadRepository;
    private readonly ITenantWriteRepository _tenantRepository;
    private readonly GetTenantByIdQueryHandler _handler;

    public GetTenantByIdQueryHandlerTests()
    {
        _tenantReadRepository = Substitute.For<ITenantReadRepository>();
        _tenantRepository = Substitute.For<ITenantWriteRepository>();
        _handler = new GetTenantByIdQueryHandler(_tenantReadRepository, _tenantRepository);
    }

    [Fact]
    public async Task Handle_ShouldReturnTenantDto_WhenTenantExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenantResult = Tenant.Create(
            CreateArgs(
                "test-tenant",
                "Test Tenant",
                "Pro",
                DatabaseStrategy.Shared,
                DatabaseProvider.PostgreSQL));

        tenantResult.IsError.ShouldBeFalse();
        var tenant = tenantResult.Value;

        // Add database metadata
        tenant.AddDatabaseMetadata(CreateMetadataArgs(
            "CatalogService",
            "ConnectionStrings__Tenants__test-tenant__Write",
            "ConnectionStrings__Tenants__test-tenant__Read",
            true));

        _tenantReadRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new TenantReadModel
            {
                Id = tenant.Id,
                Identifier = "test-tenant",
                Name = "Test Tenant",
                Plan = "Pro",
                DatabaseStrategy = "Shared",
                IsActive = true,
                CreatedAt = tenant.CreatedAt,
                UpdatedOn = tenant.UpdatedOn,
            });

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var query = new GetTenantByIdQuery(tenantId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        var dto = result.Value;
        
        dto.ShouldNotBeNull();
        dto.Id.ShouldBe(tenant.Id);
        dto.Identifier.ShouldBe("test-tenant");
        dto.Name.ShouldBe("Test Tenant");
        dto.Plan.ShouldBe("Pro");
        dto.DatabaseStrategy.ShouldBe("Shared");
        dto.IsActive.ShouldBeTrue();
        
        dto.Databases.Count.ShouldBe(1);
        var database = dto.Databases.First();
        database.ServiceName.ShouldBe("CatalogService");
        database.WriteEnvVarKey.ShouldBe("ConnectionStrings__Tenants__test-tenant__Write");
        database.ReadEnvVarKey.ShouldBe("ConnectionStrings__Tenants__test-tenant__Read");
        database.HasSeparateReadDatabase.ShouldBeTrue();

        await _tenantRepository.Received(1).GetByIdAsync(tenantId, Arg.Any<CancellationToken>());
        await _tenantReadRepository.Received(1).GetByIdAsync(tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowArgumentNullException_WhenQueryIsNull()
    {
        // Act
        async Task Action()
        {
            _ = await _handler.Handle(null!, TestContext.Current.CancellationToken);
        }

        // Assert
        await Should.ThrowAsync<ArgumentNullException>(Action);
    }

    [Fact]
    public async Task Handle_ShouldReturnError_WhenTenantNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantReadRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TenantReadModel?>(null));

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        var query = new GetTenantByIdQuery(tenantId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("Tenant.NotFound");
        result.FirstError.Description.ShouldBe($"Tenant with ID '{tenantId}' not found");

        await _tenantReadRepository.Received(1).GetByIdAsync(tenantId, Arg.Any<CancellationToken>());
        await _tenantRepository.DidNotReceive().GetByIdAsync(tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldMapMultipleDatabases_WhenTenantHasMultipleServices()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenantResult = Tenant.Create(
            CreateArgs(
                "multi-service-tenant",
                "Multi Service Tenant",
                "Enterprise",
                DatabaseStrategy.Dedicated,
                DatabaseProvider.PostgreSQL));

        var tenant = tenantResult.Value;

        // Add multiple database metadata
        tenant.AddDatabaseMetadata(CreateMetadataArgs(
            "CatalogService",
            "ConnectionStrings__Tenants__multi__Write",
            null,
            false));

        tenant.AddDatabaseMetadata(CreateMetadataArgs(
            "CustomerService",
            "ConnectionStrings__Tenants__multi__Write",
            "ConnectionStrings__Tenants__multi__Read",
            true));

        _tenantReadRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new TenantReadModel
            {
                Id = tenant.Id,
                Identifier = "multi-service-tenant",
                Name = "Multi Service Tenant",
                Plan = "Enterprise",
                DatabaseStrategy = "Dedicated",
                IsActive = true,
                CreatedAt = tenant.CreatedAt,
                UpdatedOn = tenant.UpdatedOn,
            });


        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var query = new GetTenantByIdQuery(tenantId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        var dto = result.Value;

        dto.Databases.Count.ShouldBe(2);
        dto.Databases.ShouldContain(db => db.ServiceName == "CatalogService");
        dto.Databases.ShouldContain(db => db.ServiceName == "CustomerService");

        var catalogDatabase = dto.Databases.Single(db => db.ServiceName == "CatalogService");
        catalogDatabase.ReadEnvVarKey.ShouldBeNull();
        catalogDatabase.HasSeparateReadDatabase.ShouldBeFalse();

        var customerDatabase = dto.Databases.Single(db => db.ServiceName == "CustomerService");
        customerDatabase.ReadEnvVarKey.ShouldBe("ConnectionStrings__Tenants__multi__Read");
        customerDatabase.HasSeparateReadDatabase.ShouldBeTrue();

        await _tenantRepository.Received(1).GetByIdAsync(tenantId, Arg.Any<CancellationToken>());
        await _tenantReadRepository.Received(1).GetByIdAsync(tenantId, Arg.Any<CancellationToken>());

    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyDatabases_WhenTenantHasNoMetadata()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenantResult = Tenant.Create(
            CreateArgs(
                "no-metadata-tenant",
                "No Metadata Tenant",
                "Starter",
                DatabaseStrategy.Shared,
                DatabaseProvider.PostgreSQL));

        var tenant = tenantResult.Value;

        _tenantReadRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new TenantReadModel
            {
                Id = tenant.Id,
                Identifier = "no-metadata-tenant",
                Name = "No Metadata Tenant",
                Plan = "Starter",
                DatabaseStrategy = "Shared",
                IsActive = true,
                CreatedAt = tenant.CreatedAt,
                UpdatedOn = tenant.UpdatedOn,
            });

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var query = new GetTenantByIdQuery(tenantId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Databases.ShouldBeEmpty();
        await _tenantReadRepository.Received(1).GetByIdAsync(tenantId, Arg.Any<CancellationToken>());
        await _tenantRepository.Received(1).GetByIdAsync(tenantId, Arg.Any<CancellationToken>());

    }

    private static TenantCreateArgs CreateArgs(
        string identifier,
        string name,
        string plan,
        DatabaseStrategy strategy,
        DatabaseProvider provider)
    {
        return new TenantCreateArgs
        {
            Identifier = identifier,
            Name = name,
            Plan = plan,
            Database = new TenantCreateDatabaseSettings
            {
                DatabaseStrategy = strategy,
                DatabaseProvider = provider,
            },
        };
    }

    private static TenantDatabaseMetadataArgs CreateMetadataArgs(
        string serviceName,
        string writeKey,
        string? readKey,
        bool hasSeparateReadDatabase)
    {
        return new TenantDatabaseMetadataArgs
        {
            ServiceName = serviceName,
            WriteEnvVarKey = writeKey,
            ReadEnvVarKey = readKey,
            ReadDatabaseMode = hasSeparateReadDatabase ? ReadDatabaseMode.SeparateRead : ReadDatabaseMode.SharedWrite,
        };
    }
}
