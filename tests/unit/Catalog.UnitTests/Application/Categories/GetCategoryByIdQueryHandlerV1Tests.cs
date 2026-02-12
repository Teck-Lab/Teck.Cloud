using Catalog.Application.Features.Categories.GetById.V1;
using Catalog.Application.Categories.ReadModels;
using Catalog.Application.Categories.Repositories;
using Catalog.Application.Categories.Response;
using ErrorOr;
using NSubstitute;
using Shouldly;

namespace Catalog.UnitTests.Application.Categories;

public class GetCategoryByIdQueryHandlerV1Tests
{
    private readonly ICategoryCache _cache;
    private readonly GetCategoryByIdQueryHandler _sut;

    public GetCategoryByIdQueryHandlerV1Tests()
    {
        _cache = Substitute.For<ICategoryCache>();
        _sut = new GetCategoryByIdQueryHandler(_cache);
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

        _cache.GetOrSetByIdAsync(categoryId, false, Arg.Any<CancellationToken>())
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
        
        _cache.GetOrSetByIdAsync(categoryId, false, Arg.Any<CancellationToken>())
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

        _cache.GetOrSetByIdAsync(categoryId, false, Arg.Any<CancellationToken>())
            .Returns(categoryReadModel);

        var query = new GetCategoryByIdQuery(categoryId);

        // Act
        ErrorOr<CategoryResponse> result = await _sut.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Description.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_ShouldCallCacheWithCorrectId()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var categoryReadModel = new CategoryReadModel
        {
            Id = categoryId,
            Name = "Test Category",
            Description = null
        };

        _cache.GetOrSetByIdAsync(categoryId, false, Arg.Any<CancellationToken>())
            .Returns(categoryReadModel);

        var query = new GetCategoryByIdQuery(categoryId);

        // Act
        await _sut.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        await _cache.Received(1).GetOrSetByIdAsync(categoryId, false, Arg.Any<CancellationToken>());
    }
}
