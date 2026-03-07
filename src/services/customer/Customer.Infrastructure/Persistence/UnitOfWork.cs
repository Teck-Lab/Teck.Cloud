// <copyright file="UnitOfWork.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Infrastructure.Persistence;

/// <summary>
/// Implements the Unit of Work pattern for the Customer service.
/// </summary>
public sealed class UnitOfWork : SharedKernel.Persistence.Database.EFCore.UnitOfWork<CustomerWriteDbContext>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnitOfWork"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    public UnitOfWork(CustomerWriteDbContext dbContext)
        : base(dbContext)
    {
    }
}
