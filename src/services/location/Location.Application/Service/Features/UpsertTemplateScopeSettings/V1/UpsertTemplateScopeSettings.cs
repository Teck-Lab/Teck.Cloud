// <copyright file="UpsertTemplateScopeSettings.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using Location.Application.Service.Abstractions;
using SharedKernel.Core.CQRS;

namespace Location.Application.Service.Features.UpsertTemplateScopeSettings.V1;

public sealed record UpsertTemplateScopeSettingsCommand(
    string ScopeType,
    string ScopeKey,
    string SettingsJson)
    : ICommand<ErrorOr<UpsertTemplateScopeSettingsResponse>>;

public sealed class UpsertTemplateScopeSettingsCommandHandler(ITemplateScopeSettingsWriteRepository writeRepository)
    : ICommandHandler<UpsertTemplateScopeSettingsCommand, ErrorOr<UpsertTemplateScopeSettingsResponse>>
{
    private readonly ITemplateScopeSettingsWriteRepository writeRepository = writeRepository;

    public async ValueTask<ErrorOr<UpsertTemplateScopeSettingsResponse>> Handle(
        UpsertTemplateScopeSettingsCommand request,
        CancellationToken cancellationToken)
    {
        var snapshot = new TemplateScopeSettingsSnapshot(
            TenantId: "_current",
            request.ScopeType,
            request.ScopeKey,
            request.SettingsJson);

        await this.writeRepository.UpsertAsync(snapshot, cancellationToken).ConfigureAwait(false);

        return new UpsertTemplateScopeSettingsResponse
        {
            ScopeType = request.ScopeType,
            ScopeKey = request.ScopeKey,
        };
    }
}

public sealed record UpsertTemplateScopeSettingsResponse
{
    public string ScopeType { get; init; } = string.Empty;

    public string ScopeKey { get; init; } = string.Empty;
}
