// <copyright file="DbTemplateScopeSettingsWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Location.Application.Service.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Location.Infrastructure.Persistence.Repositories.Write;

internal sealed class DbTemplateScopeSettingsWriteRepository(TemplateFontMetadataDbContext dbContext) : ITemplateScopeSettingsWriteRepository
{
    private readonly TemplateFontMetadataDbContext dbContext = dbContext;

    public async ValueTask UpsertAsync(TemplateScopeSettingsSnapshot snapshot, CancellationToken cancellationToken)
    {
        TemplateScopeSettingsRecord? existing = await this.dbContext.TemplateScopeSettings
            .FirstOrDefaultAsync(
                settings => settings.TenantId == snapshot.TenantId && settings.ScopeType == snapshot.ScopeType && settings.ScopeKey == snapshot.ScopeKey,
                cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (existing is not null)
        {
            existing.SettingsJson = snapshot.SettingsJson;
            existing.UpdatedAtUtc = now;
            this.dbContext.TemplateScopeSettings.Update(existing);
        }
        else
        {
            this.dbContext.TemplateScopeSettings.Add(new TemplateScopeSettingsRecord
            {
                Id = Guid.NewGuid(),
                TenantId = snapshot.TenantId,
                ScopeType = snapshot.ScopeType,
                ScopeKey = snapshot.ScopeKey,
                SettingsJson = snapshot.SettingsJson,
                UpdatedAtUtc = now,
            });
        }

        await this.dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DeleteAsync(string tenantId, string scopeType, string scopeKey, CancellationToken cancellationToken)
    {
        TemplateScopeSettingsRecord? existing = await this.dbContext.TemplateScopeSettings
            .FirstOrDefaultAsync(
                settings => settings.TenantId == tenantId && settings.ScopeType == scopeType && settings.ScopeKey == scopeKey,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            this.dbContext.TemplateScopeSettings.Remove(existing);
            await this.dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
