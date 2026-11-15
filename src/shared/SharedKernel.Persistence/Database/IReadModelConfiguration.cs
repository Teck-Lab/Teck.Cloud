using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Persistence.Database;

/// <summary>Marks an EF mapping that belongs only in the read model.</summary>
/// <typeparam name="TEntity">The mapped entity.</typeparam>
public interface IReadModelConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : class;
