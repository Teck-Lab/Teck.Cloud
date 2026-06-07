// <copyright file="DbTemplateDesignWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Location.Application.Service.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Location.Infrastructure.Persistence.Repositories.Write;

public sealed class DbTemplateDesignWriteRepository(TemplateFontMetadataDbContext dbContext) : ITemplateDesignWriteRepository
{
    private readonly TemplateFontMetadataDbContext dbContext = dbContext;

    public async ValueTask UpsertAsync(TemplateDesignSnapshot snapshot, CancellationToken cancellationToken)
    {
        TemplateDesignRecord? existing = await this.dbContext.TemplateDesigns
            .FirstOrDefaultAsync(
                design => design.TenantId == snapshot.TenantId && design.TemplateId == snapshot.TemplateId,
                cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (existing is not null)
        {
            existing.Name = snapshot.Name;
            existing.Width = snapshot.Width;
            existing.Height = snapshot.Height;
            existing.BackgroundColor = snapshot.BackgroundColor;
            existing.ElementsJson = snapshot.ElementsJson;
            existing.DefaultsJson = snapshot.DefaultsJson;
            existing.UpdatedAtUtc = now;
            this.dbContext.TemplateDesigns.Update(existing);
        }
        else
        {
            this.dbContext.TemplateDesigns.Add(new TemplateDesignRecord
            {
                Id = Guid.NewGuid(),
                TenantId = snapshot.TenantId,
                TemplateId = snapshot.TemplateId,
                Name = snapshot.Name,
                Width = snapshot.Width,
                Height = snapshot.Height,
                BackgroundColor = snapshot.BackgroundColor,
                ElementsJson = snapshot.ElementsJson,
                DefaultsJson = snapshot.DefaultsJson,
                UpdatedAtUtc = now,
            });
        }

        await this.dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DeleteAsync(string tenantId, string templateId, CancellationToken cancellationToken)
    {
        TemplateDesignRecord? existing = await this.dbContext.TemplateDesigns
            .FirstOrDefaultAsync(
                design => design.TenantId == tenantId && design.TemplateId == templateId,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            this.dbContext.TemplateDesigns.Remove(existing);
            await this.dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
