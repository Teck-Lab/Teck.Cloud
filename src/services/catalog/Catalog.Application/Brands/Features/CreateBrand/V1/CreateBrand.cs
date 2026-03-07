// <copyright file="CreateBrand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Brands.Mappings;
using Catalog.Domain.Entities.BrandAggregate;
using Catalog.Domain.Entities.BrandAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;

namespace Catalog.Application.Brands.Features.CreateBrand.V1
{
    /// <summary>
    /// Create brand command.
    /// </summary>
    public sealed record CreateBrandCommand(string Name, string? Description, string? Website) : ICommand<ErrorOr<CreateBrandResponse>>;

    /// <summary>
    /// Create Brand command handler.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CreateBrandCommandHandler"/> class.
    /// </remarks>
    /// <param name="unitOfWork">The unit of work.</param>
    /// <param name="brandWriteRepository">The brand repository.</param>
    internal sealed class CreateBrandCommandHandler(IUnitOfWork unitOfWork, IBrandWriteRepository brandWriteRepository) : ICommandHandler<CreateBrandCommand, ErrorOr<CreateBrandResponse>>
    {
        /// <summary>
        /// The unit of work.
        /// </summary>
        private readonly IUnitOfWork unitOfWork = unitOfWork;

        /// <summary>
        /// The brand repository.
        /// </summary>
        private readonly IBrandWriteRepository brandWriteRepository = brandWriteRepository;

        /// <summary>
        /// Handle and return a task of type erroror.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns><![CDATA[Task<ErrorOr<CreateBrandResponse>>]]></returns>
        public async ValueTask<ErrorOr<CreateBrandResponse>> Handle(CreateBrandCommand request, CancellationToken cancellationToken)
        {
            ErrorOr<Brand> brandToAdd = Brand.Create(
                request.Name!, request.Description, request.Website);

            if (brandToAdd.IsError)
            {
                return brandToAdd.Errors;
            }

            await this.brandWriteRepository.AddAsync(brandToAdd.Value, cancellationToken).ConfigureAwait(false);

            await this.unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return BrandMapper.BrandToCreateBrandResponse(brandToAdd.Value);
        }
    }
}
