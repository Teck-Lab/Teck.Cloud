using Catalog.Application.Categories.ReadModels;
using Catalog.Application.Categories.Repositories;
using Catalog.Application.Categories.Response;
using Catalog.Application.Categories.Features.GetCategoryById.V1;
using ErrorOr;
using NSubstitute;
using Shouldly;

namespace Catalog.UnitTests.Application.Categories;

public class GetCategoryByIdQueryHandlerV1Tests
{
    private readonly ICategoryReadRepository _categoryReadRepository;
    private readonly GetCategoryByIdQueryHandler _sut;

    public GetCategoryByIdQueryHandlerV1Tests()
    {
        _categoryReadRepository = Substitute.For<ICategoryReadRepository>();
        _sut = new GetCategoryByIdQueryHandler(_categoryReadRepository);
    }

    [Fact]
    public async Task Handle_ShouldReturnCategory_WhenCategoryExists()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var categoryReadModel = new CategoryReadModel
        {
            Id = categoryId,
            Name = "Test Category",
            Description = "Test Description"
        };

        _categoryReadRepository.GetByIdAsync(categoryId, Arg.Any<CancellationToken>())
            .Returns(categoryReadModel);

        var query = new GetCategoryByIdQuery(categoryId);

        // Act
        ErrorOr<CategoryResponse> result = await _sut.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Name.ShouldBe("Test Category");
        result.Value.Description.ShouldBe("Test Description");
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenCategoryDoesNotExist()
    {
        // Arrange
        var categoryId = Guid.NewGuid();

        _categoryReadRepository.GetByIdAsync(categoryId, Arg.Any<CancellationToken>())
            .Returns((CategoryReadModel?)null);

        var query = new GetCategoryByIdQuery(categoryId);

        // Act
        ErrorOr<CategoryResponse> result = await _sut.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Category.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldReturnCategoryWithNullDescription_WhenDescriptionIsNull()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var categoryReadModel = new CategoryReadModel
        {
            Id = categoryId,
            Name = "Test Category",
            Description = null
        };

        _categoryReadRepository.GetByIdAsync(categoryId, Arg.Any<CancellationToken>())
            .Returns(categoryReadModel);

        var query = new GetCategoryByIdQuery(categoryId);

        // Act
        ErrorOr<CategoryResponse> result = await _sut.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Description.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_ShouldCallRepositoryWithCorrectId()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var categoryReadModel = new CategoryReadModel
        {
            Id = categoryId,
            Name = "Test Category",
            Description = null
        };

        _categoryReadRepository.GetByIdAsync(categoryId, Arg.Any<CancellationToken>())
            .Returns(categoryReadModel);

        var query = new GetCategoryByIdQuery(categoryId);

        // Act
        await _sut.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        await _categoryReadRepository.Received(1).GetByIdAsync(categoryId, Arg.Any<CancellationToken>());
    }
}
