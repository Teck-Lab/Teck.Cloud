// <copyright file="DeleteBrands.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Domain.Entities.BrandAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Catalog.Application.Brands.Features.DeleteBrands.V1
{
    /// <summary>
    /// Delete brands command.
    /// </summary>
    public sealed record DeleteBrandsCommand(IReadOnlyCollection<Guid> BrandIds) : ICommand<ErrorOr<Deleted>>;

    /// <summary>
    /// Delete brands command handler.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="DeleteBrandsCommandHandler"/> class.
    /// </remarks>
    /// <param name="brandRepository">The brand repository.</param>
    internal sealed class DeleteBrandsCommandHandler(IBrandWriteRepository brandRepository) : ICommandHandler<DeleteBrandsCommand, ErrorOr<Deleted>>
    {
        /// <summary>
        /// The brand repository.
        /// </summary>
        private readonly IBrandWriteRepository _brandRepository = brandRepository;

        /// <summary>
        /// Handle and return a task of type erroror.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns><![CDATA[Task<ErrorOr<Deleted>>]]></returns>
        public async ValueTask<ErrorOr<Deleted>> Handle(DeleteBrandsCommand request, CancellationToken cancellationToken)
        {
            await this._brandRepository.ExcecutSoftDeleteAsync(request.BrandIds, cancellationToken).ConfigureAwait(false);

            return Result.Deleted;
        }
    }
}
