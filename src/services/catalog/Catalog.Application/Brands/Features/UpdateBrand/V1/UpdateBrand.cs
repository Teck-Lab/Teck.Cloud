using Catalog.Application.Brands.Features.Responses;
using Catalog.Application.Brands.Mappings;
using Catalog.Domain.Entities.BrandAggregate;
using Catalog.Domain.Entities.BrandAggregate.Errors;
using Catalog.Domain.Entities.BrandAggregate.Repositories;
using Catalog.Domain.Entities.BrandAggregate.Specifications;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;

namespace Catalog.Application.Brands.Features.UpdateBrand.V1
{
    /// <summary>
    /// Update brand command.
    /// </summary>
    public sealed record UpdateBrandCommand(Guid Id, string? Name, string? Description, string? Website) : ICommand<ErrorOr<BrandResponse>>;

    /// <summary>
    /// The handler.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="UpdateBrandCommandHandler"/> class.
    /// </remarks>
    /// <param name="unitOfWork">The unit of work.</param>
    /// <param name="brandRepository">The brand repository.</param>
    internal sealed class UpdateBrandCommandHandler(IUnitOfWork unitOfWork, IBrandWriteRepository brandRepository) : ICommandHandler<UpdateBrandCommand, ErrorOr<BrandResponse>>
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
        /// <returns><![CDATA[Task<ErrorOr<BrandResponse>>]]></returns>
        public async ValueTask<ErrorOr<BrandResponse>> Handle(UpdateBrandCommand request, CancellationToken cancellationToken)
        {
            var brandSpec = new BrandByIdSpecification(request.Id);
            Brand? brandToBeUpdated = await _brandRepository.FirstOrDefaultAsync(brandSpec, cancellationToken);

            if (brandToBeUpdated == null)
            {
                return BrandErrors.NotFound;
            }

            var updateOutcome = brandToBeUpdated.Update(
                request.Name, request.Description, request.Website);

            if (updateOutcome.IsError)
            {
                return updateOutcome.Errors;
            }

            _brandRepository.Update(brandToBeUpdated);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return BrandMapper.BrandToBrandResponse(brandToBeUpdated);
        }
    }
}
