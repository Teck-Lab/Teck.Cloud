// <copyright file="GetCategoryById.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Categories.Mappings;
using Catalog.Application.Categories.ReadModels;
using Catalog.Application.Categories.Repositories;
using Catalog.Application.Categories.Response;
using Catalog.Domain.Entities.CategoryAggregate.Errors;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Catalog.Application.Categories.Features.GetCategoryById.V1;

/// <summary>
/// Get a category by ID query handler.
/// </summary>
public sealed record GetCategoryByIdQuery(Guid Id) : IQuery<ErrorOr<CategoryResponse>>;

/// <summary>
/// Get brand query handler.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="GetCategoryByIdQueryHandler"/> class.
/// </remarks>
/// <param name="categoryReadRepository">The category read repository.</param>
internal sealed class GetCategoryByIdQueryHandler(ICategoryReadRepository categoryReadRepository) : IQueryHandler<GetCategoryByIdQuery, ErrorOr<CategoryResponse>>
{
    /// <summary>
    /// The category read repository.
    /// </summary>
    private readonly ICategoryReadRepository categoryReadRepository = categoryReadRepository;

    /// <summary>
    /// Handle and return a task of type erroror.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><![CDATA[Task<ErrorOr<CategoryResponse>>]]></returns>
    public async ValueTask<ErrorOr<CategoryResponse>> Handle(GetCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        CategoryReadModel? category = await this.categoryReadRepository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        return category == null ? (ErrorOr<CategoryResponse>)CategoryErrors.NotFound : (ErrorOr<CategoryResponse>)CategoryMapper.CategoryReadModelToCategoryResponse(category);
    }
}
