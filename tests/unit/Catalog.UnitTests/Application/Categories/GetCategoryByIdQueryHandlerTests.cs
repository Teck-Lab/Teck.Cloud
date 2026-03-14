using Catalog.Application.Categories.Features.GetCategoryById.V1;
using Catalog.Application.Categories.ReadModels;
using Catalog.Application.Categories.Repositories;
using Catalog.Application.Categories.Response;
using Catalog.Domain.Entities.CategoryAggregate.Errors;
using ErrorOr;
using NSubstitute;
using Shouldly;

namespace Catalog.UnitTests.Application.Categories;

public class GetCategoryByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnCategory_WhenCategoryExists()
    {
        // Arrange
        var categoryReadRepository = Substitute.For<ICategoryReadRepository>();
        var categoryId = Guid.NewGuid();
        var categoryReadModel = new CategoryReadModel
        {
            Id = categoryId,
            Name = "Test Category",
            Description = "Test Description"
        };

        categoryReadRepository.GetByIdAsync(categoryId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CategoryReadModel?>(categoryReadModel));

        var sut = new GetCategoryByIdQueryHandler(categoryReadRepository);
        var query = new GetCategoryByIdQuery(categoryId);

        // Act
        ErrorOr<CategoryResponse> result = await sut.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldNotBeNull();
        result.Value.Name.ShouldBe("Test Category");
        result.Value.Description.ShouldBe("Test Description");
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFoundError_WhenCategoryDoesNotExist()
    {
        // Arrange
        var categoryReadRepository = Substitute.For<ICategoryReadRepository>();
        var categoryId = Guid.NewGuid();

        categoryReadRepository.GetByIdAsync(categoryId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CategoryReadModel?>(null));

        var sut = new GetCategoryByIdQueryHandler(categoryReadRepository);
        var query = new GetCategoryByIdQuery(categoryId);

        // Act
        ErrorOr<CategoryResponse> result = await sut.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.First().ShouldBe(CategoryErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldCallRepository_WhenQueryIsExecuted()
    {
        // Arrange
        var categoryReadRepository = Substitute.For<ICategoryReadRepository>();
        var categoryId = Guid.NewGuid();
        var categoryReadModel = new CategoryReadModel
        {
            Id = categoryId,
            Name = "Test Category",
            Description = "Test Description"
        };

        categoryReadRepository.GetByIdAsync(categoryId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CategoryReadModel?>(categoryReadModel));

        var sut = new GetCategoryByIdQueryHandler(categoryReadRepository);
        var query = new GetCategoryByIdQuery(categoryId);

        // Act
        await sut.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        await categoryReadRepository.Received(1).GetByIdAsync(categoryId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnCategoryWithEmptyDescription_WhenDescriptionIsNull()
    {
        // Arrange
        var categoryReadRepository = Substitute.For<ICategoryReadRepository>();
        var categoryId = Guid.NewGuid();
        var categoryReadModel = new CategoryReadModel
        {
            Id = categoryId,
            Name = "Test Category",
            Description = null
        };

        categoryReadRepository.GetByIdAsync(categoryId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CategoryReadModel?>(categoryReadModel));

        var sut = new GetCategoryByIdQueryHandler(categoryReadRepository);
        var query = new GetCategoryByIdQuery(categoryId);

        // Act
        ErrorOr<CategoryResponse> result = await sut.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldNotBeNull();
        result.Value.Description.ShouldBeNull();
    }
}
