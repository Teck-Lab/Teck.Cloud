// <copyright file="UpdateSupplier.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Suppliers.Mappings;
using Catalog.Domain.Entities.SupplierAggregate;
using Catalog.Domain.Entities.SupplierAggregate.Errors;
using Catalog.Domain.Entities.SupplierAggregate.Repositories;
using Catalog.Domain.Entities.SupplierAggregate.Specifications;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;

namespace Catalog.Application.Suppliers.Features.UpdateSupplier.V1
{
    /// <summary>
    /// Update supplier command.
    /// </summary>
    public sealed record UpdateSupplierCommand(Guid Id, string? Name, string? Description, string? Website) : ICommand<ErrorOr<UpdateSupplierResponse>>;

    /// <summary>
    /// Update supplier command handler.
    /// </summary>
    internal sealed class UpdateSupplierCommandHandler(IUnitOfWork unitOfWork, ISupplierWriteRepository supplierWriteRepository) : ICommandHandler<UpdateSupplierCommand, ErrorOr<UpdateSupplierResponse>>
    {
        private readonly IUnitOfWork unitOfWork = unitOfWork;
        private readonly ISupplierWriteRepository supplierWriteRepository = supplierWriteRepository;

        /// <inheritdoc/>
        public async ValueTask<ErrorOr<UpdateSupplierResponse>> Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
        {
            SupplierByIdSpecification supplierSpecification = new(request.Id);
            Supplier? supplierToUpdate = await this.supplierWriteRepository.FirstOrDefaultAsync(supplierSpecification, cancellationToken).ConfigureAwait(false);

            if (supplierToUpdate is null)
            {
                return SupplierErrors.NotFound;
            }

            var updateOutcome = supplierToUpdate.Update(request.Name, request.Description, request.Website);

            if (updateOutcome.IsError)
            {
                return updateOutcome.Errors;
            }

            this.supplierWriteRepository.Update(supplierToUpdate);
            await this.unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return SupplierMapper.SupplierToUpdateSupplierResponse(supplierToUpdate);
        }
    }
}
