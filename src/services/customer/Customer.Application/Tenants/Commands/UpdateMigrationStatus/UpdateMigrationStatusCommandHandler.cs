using Customer.Application.Common.Interfaces;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Tenants.Commands.UpdateMigrationStatus;

/// <summary>
/// Handler for UpdateMigrationStatusCommand.
/// </summary>
public class UpdateMigrationStatusCommandHandler : ICommandHandler<UpdateMigrationStatusCommand, ErrorOr<Updated>>
{
    private readonly ITenantWriteRepository _tenantRepository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateMigrationStatusCommandHandler"/> class.
    /// </summary>
    /// <param name="tenantRepository">The tenant repository.</param>
    /// <param name="unitOfWork">The unit of work.</param>
    public UpdateMigrationStatusCommandHandler(
        ITenantWriteRepository tenantRepository,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<Updated>> Handle(UpdateMigrationStatusCommand command, CancellationToken cancellationToken)
    {
        // Get tenant
        var tenant = await _tenantRepository.GetByIdAsync(command.TenantId, cancellationToken);
        if (tenant == null)
        {
            return Error.NotFound("Tenant.NotFound", $"Tenant with ID '{command.TenantId}' not found");
        }

        // Update migration status
        var updateResult = tenant.UpdateMigrationStatus(
            command.ServiceName,
            command.Status,
            command.LastMigrationVersion,
            command.ErrorMessage);

        if (updateResult.IsError)
        {
            return updateResult.Errors;
        }

        // Save changes
        _tenantRepository.Update(tenant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Updated;
    }
}
