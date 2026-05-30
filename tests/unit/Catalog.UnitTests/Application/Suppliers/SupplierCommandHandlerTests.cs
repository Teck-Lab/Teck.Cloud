using Catalog.Application.Suppliers.Features.CreateSupplier.V1;
using Catalog.Application.Suppliers.Features.DeleteSupplier.V1;
using Catalog.Application.Suppliers.Features.GetSupplierById.V1;
using Catalog.Application.Suppliers.Features.UpdateSupplier.V1;
using Catalog.Application.Suppliers.ReadModels;
using Catalog.Application.Suppliers.Repositories;
using Catalog.Domain.Entities.SupplierAggregate;
using Catalog.Domain.Entities.SupplierAggregate.Errors;
using Catalog.Domain.Entities.SupplierAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Database;
using Shouldly;

namespace Catalog.UnitTests.Application.Suppliers;

public sealed class SupplierCommandHandlerTests
{
    [Fact]
    public async Task CreateSupplierHandle_WhenRequestIsValid_ShouldReturnSuccess()
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        ISupplierWriteRepository writeRepository = Substitute.For<ISupplierWriteRepository>();
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sut = new CreateSupplierCommandHandler(unitOfWork, writeRepository);
        CreateSupplierCommand command = new("Supplier A", "Desc", "https://example.com");

        ErrorOr<CreateSupplierResponse> result = await sut.Handle(command, TestContext.Current.CancellationToken);

        result.IsError.ShouldBeFalse();
        await writeRepository.Received(1).AddAsync(Arg.Any<Supplier>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSupplierHandle_WhenSupplierNotFound_ShouldReturnNotFoundError()
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        ISupplierWriteRepository writeRepository = Substitute.For<ISupplierWriteRepository>();
        writeRepository.FirstOrDefaultAsync(Arg.Any<Catalog.Domain.Entities.SupplierAggregate.Specifications.SupplierByIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns((Supplier?)null);

        var sut = new DeleteSupplierCommandHandler(unitOfWork, writeRepository);

        ErrorOr<Deleted> result = await sut.Handle(new DeleteSupplierCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(SupplierErrors.NotFound);
    }

    [Fact]
    public async Task GetSupplierByIdHandle_WhenSupplierNotFound_ShouldReturnNotFoundError()
    {
        ISupplierReadRepository readRepository = Substitute.For<ISupplierReadRepository>();
        readRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((SupplierReadModel?)null);

        var sut = new GetSupplierByIdQueryHandler(readRepository);

        ErrorOr<GetByIdSupplierResponse> result = await sut.Handle(new GetSupplierByIdQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(SupplierErrors.NotFound);
    }

    [Fact]
    public async Task UpdateSupplierHandle_WhenSupplierExists_ShouldReturnUpdatedResponse()
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        ISupplierWriteRepository writeRepository = Substitute.For<ISupplierWriteRepository>();
        Supplier supplier = Supplier.Create("Supplier A", "Desc", "https://example.com").Value;

        writeRepository.FirstOrDefaultAsync(Arg.Any<Catalog.Domain.Entities.SupplierAggregate.Specifications.SupplierByIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(supplier);

        var sut = new UpdateSupplierCommandHandler(unitOfWork, writeRepository);
        UpdateSupplierCommand command = new(supplier.Id, "Supplier B", "New Desc", "https://example.org");

        ErrorOr<UpdateSupplierResponse> result = await sut.Handle(command, TestContext.Current.CancellationToken);

        result.IsError.ShouldBeFalse();
        result.Value.Name.ShouldBe("Supplier B");
        writeRepository.Received(1).Update(supplier);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
