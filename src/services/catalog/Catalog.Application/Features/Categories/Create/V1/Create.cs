// <copyright file="Create.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Categories.Mappings;
using Catalog.Application.Categories.Response;
using Catalog.Domain.Entities.CategoryAggregate;
using Catalog.Domain.Entities.CategoryAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;

namespace Catalog.Application.Features.Categories.Create.V1;

/// <summary>
/// Command to create a new category.
/// </summary>
/// <param name="Name">The name of the category.</param>
/// <param name="Description">The description of the category (optional).</param>
public sealed record CreateCategoryCommand(string Name, string? Description) : ICommand<ErrorOr<CategoryResponse>>;

/// <summary>
/// Handles create category commands.
/// </summary>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="categoryWriteRepository">The category write repository.</param>
internal sealed class CreateCategoryCommandHandler(IUnitOfWork unitOfWork, ICategoryWriteRepository categoryWriteRepository) : ICommandHandler<CreateCategoryCommand, ErrorOr<CategoryResponse>>
{
    /// <summary>
    /// The unit of work.
    /// </summary>
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    private readonly ICategoryWriteRepository _categoryWriteRepository = categoryWriteRepository;

    /// <summary>
    /// Handle and return a task of type erroror.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><![CDATA[Task<ErrorOr<CategoryResponse>>]]></returns>
    public async ValueTask<ErrorOr<CategoryResponse>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        ErrorOr<Category> categoryToAdd = Category.Create(
            request.Name!, request.Description);

        if (categoryToAdd.IsError)
        {
            return categoryToAdd.Errors;
        }

        await this._categoryWriteRepository.AddAsync(categoryToAdd.Value, cancellationToken).ConfigureAwait(false);

        await this._unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return CategoryMapper.CategoryToCategoryResponse(categoryToAdd.Value);
    }
}
