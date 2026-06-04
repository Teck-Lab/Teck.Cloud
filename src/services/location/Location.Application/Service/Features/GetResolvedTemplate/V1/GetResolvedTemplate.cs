// <copyright file="GetResolvedTemplate.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using Location.Application.Service.Abstractions;
using SharedKernel.Core.CQRS;

namespace Location.Application.Service.Features.GetResolvedTemplate.V1;

/// <summary>
/// Query for resolving the effective template for a location node.
/// </summary>
/// <param name="LocationNodeId">The location node identifier.</param>
/// <param name="ExplicitTemplateId">The optional explicit template identifier override.</param>
public sealed record GetResolvedTemplateQuery(
    string LocationNodeId,
    string? ExplicitTemplateId)
    : IQuery<ErrorOr<GetResolvedTemplateResponse>>;

/// <summary>
/// Handler for <see cref="GetResolvedTemplateQuery"/>.
/// </summary>
/// <param name="resolver">Template inheritance resolver dependency.</param>
public sealed class GetResolvedTemplateQueryHandler(ITemplateInheritanceResolver resolver)
    : IQueryHandler<GetResolvedTemplateQuery, ErrorOr<GetResolvedTemplateResponse>>
{
    private readonly ITemplateInheritanceResolver resolver = resolver;

    /// <summary>
    /// Resolves the effective template and maps it to a response contract.
    /// </summary>
    /// <param name="request">The resolve template query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved template response.</returns>
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

/// <summary>
/// Response payload for a resolved template request.
/// </summary>
public sealed record GetResolvedTemplateResponse
{
    /// <summary>Gets the location node identifier.</summary>
    public string LocationNodeId { get; init; } = string.Empty;

    /// <summary>Gets the resolved template identifier.</summary>
    public string? ResolvedTemplateId { get; init; }

    /// <summary>Gets the source that produced the effective template.</summary>
    public string TemplateSource { get; init; } = "None";

    /// <summary>Gets the resolved template design response.</summary>
    public TemplateDesignResponse? TemplateDesign { get; init; }

    /// <summary>Gets effective settings by setting name.</summary>
    public IReadOnlyDictionary<string, EffectiveSettingResponse> EffectiveSettings { get; init; }
        = new Dictionary<string, EffectiveSettingResponse>();

    /// <summary>Gets the inheritance chain used during resolution.</summary>
    public IReadOnlyList<InheritanceSourceResponse> InheritanceChain { get; init; } = [];
}

/// <summary>
/// Template design response payload.
/// </summary>
public sealed record TemplateDesignResponse
{
    /// <summary>Gets the tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Gets the template identifier.</summary>
    public string TemplateId { get; init; } = string.Empty;

    /// <summary>Gets the template name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the template width.</summary>
    public int Width { get; init; }

    /// <summary>Gets the template height.</summary>
    public int Height { get; init; }

    /// <summary>Gets the background color.</summary>
    public string BackgroundColor { get; init; } = string.Empty;

    /// <summary>Gets the serialized element payload.</summary>
    public string ElementsJson { get; init; } = string.Empty;

    /// <summary>Gets the serialized defaults payload.</summary>
    public string DefaultsJson { get; init; } = "{}";
}

/// <summary>
/// Effective setting response payload.
/// </summary>
public sealed record EffectiveSettingResponse
{
    /// <summary>Gets the setting name.</summary>
    public string SettingName { get; init; } = string.Empty;

    /// <summary>Gets the effective value.</summary>
    public object? Value { get; init; }

    /// <summary>Gets the source scope type.</summary>
    public string SourceScopeType { get; init; } = string.Empty;

    /// <summary>Gets the source scope key.</summary>
    public string SourceScopeKey { get; init; } = string.Empty;

    /// <summary>Gets the effective mode.</summary>
    public string Mode { get; init; } = string.Empty;
}

/// <summary>
/// Inheritance scope response payload.
/// </summary>
public sealed record InheritanceSourceResponse
{
    /// <summary>Gets the scope type.</summary>
    public string ScopeType { get; init; } = string.Empty;

    /// <summary>Gets the scope key.</summary>
    public string ScopeKey { get; init; } = string.Empty;

    /// <summary>Gets the scope display name.</summary>
    public string ScopeName { get; init; } = string.Empty;

    /// <summary>Gets settings for the scope.</summary>
    public IReadOnlyDictionary<string, ScopedSettingResponse> Settings { get; init; }
        = new Dictionary<string, ScopedSettingResponse>();
}

/// <summary>
/// Scoped setting response payload.
/// </summary>
public sealed record ScopedSettingResponse
{
    /// <summary>Gets the setting mode.</summary>
    public string Mode { get; init; } = string.Empty;

    /// <summary>Gets the setting value.</summary>
    public object? Value { get; init; }
}
