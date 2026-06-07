// <copyright file="DbTemplateDesignReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Location.Application.Service.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Location.Infrastructure.Persistence.Repositories.Read;

public sealed class DbTemplateDesignReadRepository(LocationReadDbContext dbContext) : ITemplateDesignReadRepository
{
    private readonly LocationReadDbContext dbContext = dbContext;

    public async ValueTask<TemplateDesignSnapshot?> GetByTemplateIdAsync(
        string tenantId,
        string templateId,
        CancellationToken cancellationToken)
    {
        TemplateDesignRecord? record = await this.dbContext.TemplateDesigns
            .AsNoTracking()
            .FirstOrDefaultAsync(
                design => design.TenantId == tenantId && design.TemplateId == templateId,
                cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return null;
        }

        return MapToSnapshot(record);
    }

    public async ValueTask<IReadOnlyList<TemplateDesignSnapshot>> ListByTenantAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        List<TemplateDesignRecord> records = await this.dbContext.TemplateDesigns
            .AsNoTracking()
            .Where(design => design.TenantId == tenantId)
            .OrderBy(design => design.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return records.Select(MapToSnapshot).ToList();
    }

    private static TemplateDesignSnapshot MapToSnapshot(TemplateDesignRecord record)
    {
        return new TemplateDesignSnapshot(
            record.TenantId,
            record.TemplateId,
            record.Name,
            record.Width,
            record.Height,
            record.BackgroundColor,
            record.ElementsJson,
            record.DefaultsJson);
    }
}
