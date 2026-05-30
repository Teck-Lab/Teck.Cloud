// <copyright file="DbTemplateScopeSettingsReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Location.Application.Service.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Location.Infrastructure.Persistence.Repositories.Read;

internal sealed class DbTemplateScopeSettingsReadRepository(LocationReadDbContext dbContext) : ITemplateScopeSettingsReadRepository
{
    private readonly LocationReadDbContext dbContext = dbContext;

    public async ValueTask<TemplateScopeSettingsSnapshot?> GetByScopeAsync(
        string tenantId,
        string scopeType,
        string scopeKey,
        CancellationToken cancellationToken)
    {
        TemplateScopeSettingsRecord? record = await this.dbContext.TemplateScopeSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                settings => settings.TenantId == tenantId && settings.ScopeType == scopeType && settings.ScopeKey == scopeKey,
                cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return null;
        }

        return new TemplateScopeSettingsSnapshot(
            record.TenantId,
            record.ScopeType,
            record.ScopeKey,
            record.SettingsJson);
    }
}
