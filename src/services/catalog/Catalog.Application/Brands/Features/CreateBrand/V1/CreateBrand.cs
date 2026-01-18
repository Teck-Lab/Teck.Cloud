using Catalog.Application.Brands.Features.Responses;
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
    public sealed record CreateBrandCommand(string Name, string? Description, string? Website) : ICommand<ErrorOr<BrandResponse>>;

    /// <summary>
    /// Create Brand command handler.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CreateBrandCommandHandler"/> class.
    /// </remarks>
    /// <param name="unitOfWork">The unit of work.</param>
    /// <param name="brandWriteRepository">The brand repository.</param>
    internal sealed class CreateBrandCommandHandler(IUnitOfWork unitOfWork, IBrandWriteRepository brandWriteRepository) : ICommandHandler<CreateBrandCommand, ErrorOr<BrandResponse>>
    {
        /// <summary>
        /// The unit of work.
        /// </summary>
        private readonly IUnitOfWork _unitOfWork = unitOfWork;

        /// <summary>
        /// The brand repository.
        /// </summary>
        private readonly IBrandWriteRepository _brandWriteRepository = brandWriteRepository;

        /// <summary>
        /// Handle and return a task of type erroror.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns><![CDATA[Task<ErrorOr<BrandResponse>>]]></returns>
        public async ValueTask<ErrorOr<BrandResponse>> Handle(CreateBrandCommand request, CancellationToken cancellationToken)
        {
            ErrorOr<Brand> brandToAdd = Brand.Create(
                request.Name!, request.Description, request.Website);

            if (brandToAdd.IsError)
            {
                return brandToAdd.Errors;
            }

            await _brandWriteRepository.AddAsync(brandToAdd.Value, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return BrandMapper.BrandToBrandResponse(brandToAdd.Value);
        }
    }
}
