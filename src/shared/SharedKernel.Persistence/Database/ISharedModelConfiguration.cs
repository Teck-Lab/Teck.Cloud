using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Persistence.Database;

/// <summary>Marks an EF mapping that belongs in both read and write models.</summary>
/// <typeparam name="TEntity">The mapped entity.</typeparam>
public interface ISharedModelConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : class;
