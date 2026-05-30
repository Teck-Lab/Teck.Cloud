// <copyright file="GetResolvedTemplate.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using Location.Application.Service.Abstractions;
using SharedKernel.Core.CQRS;

namespace Location.Application.Service.Features.GetResolvedTemplate.V1;

public sealed record GetResolvedTemplateQuery(
    string LocationNodeId,
    string? ExplicitTemplateId)
    : IQuery<ErrorOr<GetResolvedTemplateResponse>>;

public sealed class GetResolvedTemplateQueryHandler(ITemplateInheritanceResolver resolver)
    : IQueryHandler<GetResolvedTemplateQuery, ErrorOr<GetResolvedTemplateResponse>>
{
    private readonly ITemplateInheritanceResolver resolver = resolver;

    public async ValueTask<ErrorOr<GetResolvedTemplateResponse>> Handle(
        GetResolvedTemplateQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.LocationNodeId);

        ResolvedTemplateContext context = await this.resolver
            .ResolveAsync("_current", request.LocationNodeId, request.ExplicitTemplateId, cancellationToken)
            .ConfigureAwait(false);

        return new GetResolvedTemplateResponse
        {
            LocationNodeId = context.LocationNodeId,
            ResolvedTemplateId = context.ResolvedTemplateId,
            TemplateSource = context.TemplateSource,
            TemplateDesign = context.ResolvedTemplateDesign is null
                ? null
                : new TemplateDesignResponse
                {
                    TenantId = context.ResolvedTemplateDesign.TenantId,
                    TemplateId = context.ResolvedTemplateDesign.TemplateId,
                    Name = context.ResolvedTemplateDesign.Name,
                    Width = context.ResolvedTemplateDesign.Width,
                    Height = context.ResolvedTemplateDesign.Height,
                    BackgroundColor = context.ResolvedTemplateDesign.BackgroundColor,
                    ElementsJson = context.ResolvedTemplateDesign.ElementsJson,
                    DefaultsJson = context.ResolvedTemplateDesign.DefaultsJson,
                },
            EffectiveSettings = context.EffectiveSettings.ToDictionary(
                kvp => kvp.Key,
                kvp => new EffectiveSettingResponse
                {
                    SettingName = kvp.Value.SettingName,
                    Value = kvp.Value.Value,
                    SourceScopeType = kvp.Value.SourceScopeType,
                    SourceScopeKey = kvp.Value.SourceScopeKey,
                    Mode = kvp.Value.Mode,
                }),
            InheritanceChain = context.InheritanceChain.Select(source => new InheritanceSourceResponse
            {
                ScopeType = source.ScopeType,
                ScopeKey = source.ScopeKey,
                ScopeName = source.ScopeName,
                Settings = source.Settings.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new ScopedSettingResponse
                    {
                        Mode = kvp.Value.Mode,
                        Value = kvp.Value.Value,
                    }),
            }).ToList(),
        };
    }
}

public sealed record GetResolvedTemplateResponse
{
    public string LocationNodeId { get; init; } = string.Empty;

    public string? ResolvedTemplateId { get; init; }

    public string TemplateSource { get; init; } = "None";

    public TemplateDesignResponse? TemplateDesign { get; init; }

    public IReadOnlyDictionary<string, EffectiveSettingResponse> EffectiveSettings { get; init; }
        = new Dictionary<string, EffectiveSettingResponse>();

    public IReadOnlyList<InheritanceSourceResponse> InheritanceChain { get; init; } = [];
}

public sealed record TemplateDesignResponse
{
    public string TenantId { get; init; } = string.Empty;

    public string TemplateId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int Width { get; init; }

    public int Height { get; init; }

    public string BackgroundColor { get; init; } = string.Empty;

    public string ElementsJson { get; init; } = string.Empty;

    public string DefaultsJson { get; init; } = "{}";
}

public sealed record EffectiveSettingResponse
{
    public string SettingName { get; init; } = string.Empty;

    public object? Value { get; init; }

    public string SourceScopeType { get; init; } = string.Empty;

    public string SourceScopeKey { get; init; } = string.Empty;

    public string Mode { get; init; } = string.Empty;
}

public sealed record InheritanceSourceResponse
{
    public string ScopeType { get; init; } = string.Empty;

    public string ScopeKey { get; init; } = string.Empty;

    public string ScopeName { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, ScopedSettingResponse> Settings { get; init; }
        = new Dictionary<string, ScopedSettingResponse>();
}

public sealed record ScopedSettingResponse
{
    public string Mode { get; init; } = string.Empty;

    public object? Value { get; init; }
}
