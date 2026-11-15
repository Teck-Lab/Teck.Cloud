using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Persistence.Database;

/// <summary>Marks an EF mapping that belongs only in the write model and its migrations.</summary>
/// <typeparam name="TEntity">The mapped entity.</typeparam>
public interface IWriteModelConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : class;
