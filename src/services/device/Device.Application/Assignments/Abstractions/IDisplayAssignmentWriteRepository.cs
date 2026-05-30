// <copyright file="IDisplayAssignmentWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DisplayAssignmentAggregate;
using SharedKernel.Core.Database;

namespace Device.Application.Assignments.Abstractions;

/// <summary>
/// Write-side repository for <see cref="DisplayAssignment"/> aggregates.
/// Inherits the standard CRUD + specification surface from
/// <see cref="IGenericWriteRepository{TEntity, TId}"/> for consistency with the
/// rest of the Device write-side repositories.
/// </summary>
public interface IDisplayAssignmentWriteRepository : IGenericWriteRepository<DisplayAssignment, Guid>
{
}
