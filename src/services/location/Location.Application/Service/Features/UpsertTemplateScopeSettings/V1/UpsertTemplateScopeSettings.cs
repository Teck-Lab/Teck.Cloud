// <copyright file="UpsertTemplateScopeSettings.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using Location.Application.Service.Abstractions;
using SharedKernel.Core.CQRS;

namespace Location.Application.Service.Features.UpsertTemplateScopeSettings.V1;

/// <summary>
/// Command for creating or updating template scope settings.
/// </summary>
/// <param name="ScopeType">The scope type.</param>
/// <param name="ScopeKey">The scope key.</param>
/// <param name="SettingsJson">Serialized settings payload.</param>
public sealed record UpsertTemplateScopeSettingsCommand(
    string ScopeType,
    string ScopeKey,
    string SettingsJson)
    : ICommand<ErrorOr<UpsertTemplateScopeSettingsResponse>>;

/// <summary>
/// Handler for <see cref="UpsertTemplateScopeSettingsCommand"/>.
/// </summary>
/// <param name="writeRepository">Template scope settings write repository dependency.</param>
public sealed class UpsertTemplateScopeSettingsCommandHandler(ITemplateScopeSettingsWriteRepository writeRepository)
    : ICommandHandler<UpsertTemplateScopeSettingsCommand, ErrorOr<UpsertTemplateScopeSettingsResponse>>
{
    private readonly ITemplateScopeSettingsWriteRepository writeRepository = writeRepository;

    /// <summary>
    /// Creates or updates template scope settings.
    /// </summary>
    /// <param name="request">The upsert scope settings command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The upsert scope settings response.</returns>
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

/// <summary>
/// Response payload for template scope settings upsert.
/// </summary>
public sealed record UpsertTemplateScopeSettingsResponse
{
    /// <summary>Gets the scope type.</summary>
    public string ScopeType { get; init; } = string.Empty;

    /// <summary>Gets the scope key.</summary>
    public string ScopeKey { get; init; } = string.Empty;
}
