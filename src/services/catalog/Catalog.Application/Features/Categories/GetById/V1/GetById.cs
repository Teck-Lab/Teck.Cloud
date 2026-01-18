using Catalog.Application.Brands.Mappings;
using Catalog.Application.Categories.ReadModels;
using Catalog.Application.Categories.Repositories;
using Catalog.Application.Categories.Response;
using Catalog.Domain.Entities.CategoryAggregate.Errors;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Catalog.Application.Features.Categories.GetById.V1;

/// <summary>
/// Get a category by ID query handler.
/// </summary>
public sealed record GetCategoryByIdQuery(Guid Id) : IQuery<ErrorOr<CategoryResponse>>;

/// <summary>
/// Get category query handler.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="GetCategoryByIdQueryHandler"/> class.
/// </remarks>
/// <param name="cache">The cache.</param>
internal sealed class GetCategoryByIdQueryHandler(ICategoryCache cache) : IQueryHandler<GetCategoryByIdQuery, ErrorOr<CategoryResponse>>
{
    /// <summary>
    /// The cache.
    /// </summary>
    private readonly ICategoryCache _cache = cache;

    /// <summary>
    /// Handle and return a task of type erroror.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><![CDATA[Task<ErrorOr<CategoryResponse>>]]></returns>
    public async ValueTask<ErrorOr<CategoryResponse>> Handle(GetCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        CategoryReadModel? category = await _cache.GetOrSetByIdAsync(request.Id, cancellationToken: cancellationToken);

        return category == null ? (ErrorOr<CategoryResponse>)CategoryErrors.NotFound : (ErrorOr<CategoryResponse>)CategoryMapper.CategoryReadModelToCategoryResponse(category);
    }
}
