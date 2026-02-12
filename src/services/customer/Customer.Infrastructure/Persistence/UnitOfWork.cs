using Customer.Application.Common.Interfaces;

namespace Customer.Infrastructure.Persistence;

/// <summary>
/// Implements the Unit of Work pattern for the Customer service.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly CustomerWriteDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitOfWork"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    public UnitOfWork(CustomerWriteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
