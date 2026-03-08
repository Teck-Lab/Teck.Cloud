// <copyright file="DeleteBrand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Domain.Entities.BrandAggregate;
using Catalog.Domain.Entities.BrandAggregate.Errors;
using Catalog.Domain.Entities.BrandAggregate.Repositories;
using Catalog.Domain.Entities.BrandAggregate.Specifications;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;

namespace Catalog.Application.Brands.Features.DeleteBrand.V1
{
    /// <summary>
    /// Delete brand command.
    /// </summary>
    public sealed record DeleteBrandCommand(Guid Id) : ICommand<ErrorOr<Deleted>>;

    /// <summary>
    /// Delete Brand command handler.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="DeleteBrandCommandHandler"/> class.
    /// </remarks>
    /// <param name="unitOfWork">The unit of work.</param>
    /// <param name="brandRepository">The brand repository.</param>
    internal sealed class DeleteBrandCommandHandler(IUnitOfWork unitOfWork, IBrandWriteRepository brandRepository) : ICommandHandler<DeleteBrandCommand, ErrorOr<Deleted>>
    {
        /// <summary>
        /// The unit of work.
        /// </summary>
        private readonly IUnitOfWork _unitOfWork = unitOfWork;

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
        public async ValueTask<ErrorOr<Deleted>> Handle(DeleteBrandCommand request, CancellationToken cancellationToken)
        {
            var brandSpec = new BrandByIdSpecification(request.Id);
            Brand? brandToDelete = await this._brandRepository.FirstOrDefaultAsync(brandSpec, cancellationToken).ConfigureAwait(false);

            if (brandToDelete is null)
            {
                return BrandErrors.NotFound;
            }

            this._brandRepository.Delete(brandToDelete);
            await this._unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return Result.Deleted;
        }
    }
}
