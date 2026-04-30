// <copyright file="CreateSupplier.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Suppliers.Mappings;
using Catalog.Domain.Entities.SupplierAggregate;
using Catalog.Domain.Entities.SupplierAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;

namespace Catalog.Application.Suppliers.Features.CreateSupplier.V1
{
    /// <summary>
    /// Create supplier command.
    /// </summary>
    public sealed record CreateSupplierCommand(string Name, string? Description, string? Website) : ICommand<ErrorOr<CreateSupplierResponse>>;

    /// <summary>
    /// Create supplier command handler.
    /// </summary>
    internal sealed class CreateSupplierCommandHandler(IUnitOfWork unitOfWork, ISupplierWriteRepository supplierWriteRepository) : ICommandHandler<CreateSupplierCommand, ErrorOr<CreateSupplierResponse>>
    {
        private readonly IUnitOfWork unitOfWork = unitOfWork;
        private readonly ISupplierWriteRepository supplierWriteRepository = supplierWriteRepository;

        /// <inheritdoc/>
        public async ValueTask<ErrorOr<CreateSupplierResponse>> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
        {
            ErrorOr<Supplier> supplierToAdd = Supplier.Create(request.Name, request.Description, request.Website);

            if (supplierToAdd.IsError)
            {
                return supplierToAdd.Errors;
            }

            await this.supplierWriteRepository.AddAsync(supplierToAdd.Value, cancellationToken).ConfigureAwait(false);
            await this.unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return SupplierMapper.SupplierToCreateSupplierResponse(supplierToAdd.Value);
        }
    }
}
