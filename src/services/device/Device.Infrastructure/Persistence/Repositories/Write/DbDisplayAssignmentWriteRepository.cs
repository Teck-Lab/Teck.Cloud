// <copyright file="DbDisplayAssignmentWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Assignments.Abstractions;
using Device.Domain.Entities.DisplayAssignmentAggregate;
using Microsoft.AspNetCore.Http;
using SharedKernel.Persistence.Database.EFCore;

namespace Device.Infrastructure.Persistence.Repositories.Write;

public sealed class DbDisplayAssignmentWriteRepository(
    DeviceWriteDbContext dbContext,
    IHttpContextAccessor httpContextAccessor)
    : GenericWriteRepository<DisplayAssignment, Guid, DeviceWriteDbContext>(dbContext, httpContextAccessor),
      IDisplayAssignmentWriteRepository
{
}
