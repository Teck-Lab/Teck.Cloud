// <copyright file="DbDisplayAssignmentReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Assignments.Abstractions;
using Device.Domain.Entities.DisplayAssignmentAggregate;
using Microsoft.EntityFrameworkCore;

namespace Device.Infrastructure.Persistence.Repositories.Read;

public sealed class DbDisplayAssignmentReadRepository(DeviceReadDbContext dbContext)
    : IDisplayAssignmentReadRepository
{
    private readonly DeviceReadDbContext dbContext = dbContext;

    /// <inheritdoc/>
    public async ValueTask<DisplayAssignmentSummary?> GetSummaryByIdAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        DisplayAssignmentSummary? summary = await this.dbContext
            .Set<DisplayAssignment>()
            .AsNoTracking()
            .Where(assignment => assignment.Id == assignmentId)
            .Select(assignment => new
            {
                assignment.Id,
                assignment.DisplayId,
                assignment.ResolvedTemplateId,
                StatusName = assignment.Status.ToString(),
                assignment.RenderJobId,
                assignment.RenderedImageUri,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) is { } row
                ? new DisplayAssignmentSummary(
                    row.Id,
                    row.DisplayId,
                    row.ResolvedTemplateId,
                    row.StatusName,
                    row.RenderJobId,
                    row.RenderedImageUri)
                : null;

        return summary;
    }
}
