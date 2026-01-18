using Catalog.Application.Brands.Repositories;
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
    /// <param name="cache">The cache.</param>
    /// <param name="brandRepository">The brand repository.</param>
    internal sealed class DeleteBrandsCommandHandler(IBrandCache cache, IBrandWriteRepository brandRepository) : ICommandHandler<DeleteBrandsCommand, ErrorOr<Deleted>>
    {
        /// <summary>
        /// The brand repository.
        /// </summary>
        private readonly IBrandWriteRepository _brandRepository = brandRepository;

        /// <summary>
        /// The cache.
        /// </summary>
        private readonly IBrandCache _cache = cache;

        /// <summary>
        /// Handle and return a task of type erroror.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns><![CDATA[Task<ErrorOr<Deleted>>]]></returns>
        public async ValueTask<ErrorOr<Deleted>> Handle(DeleteBrandsCommand request, CancellationToken cancellationToken)
        {
            await _brandRepository.ExcecutSoftDeleteAsync(request.BrandIds, cancellationToken);
            foreach (Guid id in request.BrandIds)
            {
                await _cache.RemoveAsync(id, cancellationToken);
            }

            return Result.Deleted;
        }
    }
}
