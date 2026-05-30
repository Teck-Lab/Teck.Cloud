// <copyright file="ITemplateDesignWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Abstractions;

public interface ITemplateDesignWriteRepository
{
    ValueTask UpsertAsync(TemplateDesignSnapshot snapshot, CancellationToken cancellationToken);

    ValueTask DeleteAsync(string tenantId, string templateId, CancellationToken cancellationToken);
}
