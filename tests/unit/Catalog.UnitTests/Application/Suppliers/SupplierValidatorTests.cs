using System.Linq.Expressions;
using Catalog.Application.Suppliers.Features.CreateSupplier.V1;
using Catalog.Application.Suppliers.Features.DeleteSupplier.V1;
using Catalog.Application.Suppliers.Features.GetPaginatedSuppliers.V1;
using Catalog.Application.Suppliers.Features.GetSupplierById.V1;
using Catalog.Application.Suppliers.Features.UpdateSupplier.V1;
using Catalog.Application.Suppliers.ReadModels;
using Catalog.Application.Suppliers.Repositories;
using NSubstitute;
using Shouldly;

namespace Catalog.UnitTests.Application.Suppliers;

public sealed class SupplierValidatorTests
{
    [Fact]
    public async Task CreateSupplierValidator_WhenNameAlreadyExists_ShouldFailValidation()
    {
        ISupplierReadRepository readRepository = Substitute.For<ISupplierReadRepository>();
        readRepository.ExistsAsync(Arg.Any<Expression<Func<SupplierReadModel, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(true);

        CreateSupplierValidator sut = new(readRepository);

        var result = await sut.ValidateAsync(new CreateSupplierRequest { Name = "Supplier A" }, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task GetPaginatedSuppliersValidator_WhenPageAndSizeAreValid_ShouldPassValidation()
    {
        GetPaginatedSuppliersValidator sut = new();

        var result = await sut.ValidateAsync(new GetPaginatedSuppliersRequest { Page = 1, Size = 10 }, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task GetSupplierByIdValidator_WhenIdEmpty_ShouldFailValidation()
    {
        GetSupplierByIdValidator sut = new();

        var result = await sut.ValidateAsync(new GetSupplierByIdRequest { Id = Guid.Empty }, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteSupplierValidator_WhenIdEmpty_ShouldFailValidation()
    {
        DeleteSupplierValidator sut = new();

        var result = await sut.ValidateAsync(new DeleteSupplierRequest { Id = Guid.Empty }, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateSupplierValidator_WhenNameMissing_ShouldFailValidation()
    {
        UpdateSupplierValidator sut = new();

        var result = await sut.ValidateAsync(new UpdateSupplierRequest { Id = Guid.NewGuid(), Name = string.Empty }, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }
}
