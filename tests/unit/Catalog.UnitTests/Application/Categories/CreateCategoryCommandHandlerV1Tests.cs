using Catalog.Application.Features.Categories.Create.V1;
using Catalog.Application.Categories.Response;
using Catalog.Domain.Entities.CategoryAggregate;
using Catalog.Domain.Entities.CategoryAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Database;
using Shouldly;

namespace Catalog.UnitTests.Application.Categories;

public class CreateCategoryCommandHandlerV1Tests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICategoryWriteRepository _categoryRepository;
    private readonly CreateCategoryCommandHandler _sut;

    public CreateCategoryCommandHandlerV1Tests()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _categoryRepository = Substitute.For<ICategoryWriteRepository>();
        _sut = new CreateCategoryCommandHandler(_unitOfWork, _categoryRepository);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenCategoryIsValid()
    {
        // Arrange
        var command = new CreateCategoryCommand("Test Category", "Test Description");
        
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        ErrorOr<CategoryResponse> result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldNotBeNull();
        result.Value.Name.ShouldBe("Test Category");
        result.Value.Description.ShouldBe("Test Description");
        
        await _categoryRepository.Received(1).AddAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnError_WhenDescriptionIsEmpty()
    {
        // Arrange
        var command = new CreateCategoryCommand("Test Category", "");

        // Act
        ErrorOr<CategoryResponse> result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Category.EmptyDescription");
        
        await _categoryRepository.DidNotReceive().AddAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnError_WhenNameIsEmpty()
    {
        // Arrange
        var command = new CreateCategoryCommand("", "Description");

        // Act
        ErrorOr<CategoryResponse> result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Category.EmptyName");
        
        await _categoryRepository.DidNotReceive().AddAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnError_WhenNameIsWhitespace()
    {
        // Arrange
        var command = new CreateCategoryCommand("   ", "Description");

        // Act
        ErrorOr<CategoryResponse> result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Category.EmptyName");
    }
}
