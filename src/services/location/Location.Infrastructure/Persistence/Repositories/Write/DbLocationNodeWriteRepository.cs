// <copyright file="DbLocationNodeWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Location.Application.Service.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Location.Infrastructure.Persistence.Repositories.Write;

public sealed class DbLocationNodeWriteRepository(TemplateFontMetadataDbContext dbContext) : ILocationNodeWriteRepository
{
    private readonly TemplateFontMetadataDbContext dbContext = dbContext;

    public async ValueTask CreateAsync(LocationNodeSnapshot snapshot, CancellationToken cancellationToken)
    {
        this.dbContext.LocationNodes.Add(new LocationNodeRecord
        {
            Id = Guid.NewGuid(),
            LocationNodeId = snapshot.LocationNodeId,
            ParentLocationNodeId = snapshot.ParentLocationNodeId,
            Name = snapshot.Name,
            TemplateId = snapshot.TemplateId,
            LocationGroupId = snapshot.LocationGroupId,
            Aisle = snapshot.Aisle,
            Shelf = snapshot.Shelf,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });

        await this.dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<bool> ExistsAsync(string tenantId, string locationNodeId, CancellationToken cancellationToken)
    {
        return await this.dbContext.LocationNodes
            .AnyAsync(
                node => node.LocationNodeId == locationNodeId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<bool> NameExistsAsync(string tenantId, string name, CancellationToken cancellationToken)
    {
        return await this.dbContext.LocationNodes
            .AnyAsync(
                node => node.Name == name,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
