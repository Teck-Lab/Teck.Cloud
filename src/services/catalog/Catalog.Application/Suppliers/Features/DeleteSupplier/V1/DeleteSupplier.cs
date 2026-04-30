// <copyright file="DeleteSupplier.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Domain.Entities.SupplierAggregate;
using Catalog.Domain.Entities.SupplierAggregate.Errors;
using Catalog.Domain.Entities.SupplierAggregate.Repositories;
using Catalog.Domain.Entities.SupplierAggregate.Specifications;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;

namespace Catalog.Application.Suppliers.Features.DeleteSupplier.V1
{
    /// <summary>
    /// Delete supplier command.
    /// </summary>
    public sealed record DeleteSupplierCommand(Guid Id) : ICommand<ErrorOr<Deleted>>;

    /// <summary>
    /// Delete supplier command handler.
    /// </summary>
    internal sealed class DeleteSupplierCommandHandler(IUnitOfWork unitOfWork, ISupplierWriteRepository supplierWriteRepository) : ICommandHandler<DeleteSupplierCommand, ErrorOr<Deleted>>
    {
        private readonly IUnitOfWork unitOfWork = unitOfWork;
        private readonly ISupplierWriteRepository supplierWriteRepository = supplierWriteRepository;

        /// <inheritdoc/>
        public async ValueTask<ErrorOr<Deleted>> Handle(DeleteSupplierCommand request, CancellationToken cancellationToken)
        {
            SupplierByIdSpecification supplierSpecification = new(request.Id);
            Supplier? supplierToDelete = await this.supplierWriteRepository.FirstOrDefaultAsync(supplierSpecification, cancellationToken).ConfigureAwait(false);

            if (supplierToDelete is null)
            {
                return SupplierErrors.NotFound;
            }

            this.supplierWriteRepository.Delete(supplierToDelete);
            await this.unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return Result.Deleted;
        }
    }
}
