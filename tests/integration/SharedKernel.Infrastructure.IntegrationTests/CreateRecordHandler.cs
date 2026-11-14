using ErrorOr;
using SharedKernel.Core.Database;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Actual handler used by Wolverine generated code.</summary>
public static class CreateRecordHandler
{
    /// <summary>Mutates an aggregate; Wolverine owns the commit and event collection.</summary>
    /// <param name="command">The request.</param>
    /// <param name="repository">Scoped write repository.</param>
    /// <param name="unitOfWork">Application save boundary.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>A typed result retained by InvokeAsync.</returns>
    /// <exception cref="InvalidOperationException">The test requested rollback.</exception>
    public static async Task<ErrorOr<Guid>> HandleAsync(CreateRecord command, IGenericWriteRepository<StoredRecord, Guid> repository, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var record = new StoredRecord(command.Id);
        record.AddDomainEvent(new CreatedLocally { RecordId = command.Id });
        await repository.AddAsync(record, cancellationToken).ConfigureAwait(false);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return command.Fail ? throw new InvalidOperationException("Requested test rollback.") : command.Id;
    }
}
