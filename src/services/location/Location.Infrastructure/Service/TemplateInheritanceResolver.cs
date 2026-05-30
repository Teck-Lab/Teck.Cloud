// <copyright file="TemplateInheritanceResolver.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Text.Json.Nodes;
using Location.Application.Service.Abstractions;

namespace Location.Infrastructure.Service;

/// <summary>
/// Implements template inheritance resolution: Tenant → Location Group → Location → Ancestors.
/// </summary>
internal sealed class TemplateInheritanceResolver(
    ITemplateScopeSettingsReadRepository scopeSettingsRepository,
    ITemplateDesignReadRepository templateDesignRepository,
    ILocationNodeReadRepository locationNodeReadRepository,
    ILocationGroupReadRepository locationGroupReadRepository)
    : ITemplateInheritanceResolver
{
    public async ValueTask<ResolvedTemplateContext> ResolveAsync(
        string tenantId,
        string locationNodeId,
        string? explicitTemplateId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationNodeId);

        // Build inheritance chain
        List<InheritanceSource> chain = [];
        Dictionary<string, EffectiveSettingValue> effectiveSettings = new(StringComparer.Ordinal);

        // 1. Tenant scope
        TemplateScopeSettingsSnapshot? tenantSettings = await scopeSettingsRepository
            .GetByScopeAsync(tenantId, "Tenant", "_tenant", cancellationToken)
            .ConfigureAwait(false);

        if (tenantSettings is not null)
        {
            Dictionary<string, ScopedSetting> tenantScopedSettings = ParseSettingsJson(tenantSettings.SettingsJson);
            chain.Add(new InheritanceSource("Tenant", "_tenant", "Tenant Defaults", tenantScopedSettings));
            ApplySettings(tenantScopedSettings, effectiveSettings, "Tenant", "_tenant");
        }

        // 2. Resolve location and optional group
        LocationNodeSnapshot? locationNode = await locationNodeReadRepository
            .GetByIdAsync(locationNodeId, cancellationToken)
            .ConfigureAwait(false);

        string? locationGroupId = null;
        if (locationNode?.LocationGroupId is not null)
        {
            locationGroupId = locationNode.LocationGroupId;
        }

        // 3. Location Group scope (if assigned)
        if (!string.IsNullOrWhiteSpace(locationGroupId))
        {
            LocationGroupSnapshot? group = await locationGroupReadRepository
                .GetByIdAsync(tenantId, locationGroupId, cancellationToken)
                .ConfigureAwait(false);

            TemplateScopeSettingsSnapshot? groupSettings = await scopeSettingsRepository
                .GetByScopeAsync(tenantId, "LocationGroup", locationGroupId, cancellationToken)
                .ConfigureAwait(false);

            if (groupSettings is not null && group is not null)
            {
                Dictionary<string, ScopedSetting> groupScopedSettings = ParseSettingsJson(groupSettings.SettingsJson);
                chain.Add(new InheritanceSource("LocationGroup", locationGroupId, group.Name, groupScopedSettings));
                ApplySettings(groupScopedSettings, effectiveSettings, "LocationGroup", locationGroupId);
            }
        }

        // 4. Location scope + ancestor chain
        HashSet<string> visitedNodes = new(StringComparer.Ordinal);
        string? currentNodeId = locationNodeId;
        int ancestorDepth = 0;
        const int maxAncestorDepth = 32;

        while (!string.IsNullOrWhiteSpace(currentNodeId))
        {
            if (!visitedNodes.Add(currentNodeId))
            {
                break;
            }

            LocationNodeSnapshot? node = await locationNodeReadRepository
                .GetByIdAsync(currentNodeId, cancellationToken)
                .ConfigureAwait(false);

            if (node is null)
            {
                break;
            }

            TemplateScopeSettingsSnapshot? nodeSettings = await scopeSettingsRepository
                .GetByScopeAsync(tenantId, "Location", currentNodeId, cancellationToken)
                .ConfigureAwait(false);

            if (nodeSettings is not null)
            {
                Dictionary<string, ScopedSetting> nodeScopedSettings = ParseSettingsJson(nodeSettings.SettingsJson);
                string scopeName = node.Name ?? currentNodeId;
                chain.Add(new InheritanceSource(
                    ancestorDepth == 0 ? "Location" : "Ancestor",
                    currentNodeId,
                    scopeName,
                    nodeScopedSettings));
                ApplySettings(nodeScopedSettings, effectiveSettings, ancestorDepth == 0 ? "Location" : "Ancestor", currentNodeId);
            }

            if (string.IsNullOrWhiteSpace(node.ParentLocationNodeId) || ancestorDepth >= maxAncestorDepth)
            {
                break;
            }

            ancestorDepth++;
            currentNodeId = node.ParentLocationNodeId;
        }

        // 5. Determine resolved template id
        string? resolvedTemplateId = explicitTemplateId;
        string templateSource = "Request";

        if (string.IsNullOrWhiteSpace(resolvedTemplateId))
        {
            // Check effective settings for templateId
            if (effectiveSettings.TryGetValue("templateId", out EffectiveSettingValue? templateSetting))
            {
                resolvedTemplateId = templateSetting.Value?.ToString();
                templateSource = $"{templateSetting.SourceScopeType}Setting";
            }

            // Fallback: walk location chain for direct TemplateId on LocationNodeRecord
            if (string.IsNullOrWhiteSpace(resolvedTemplateId))
            {
                (resolvedTemplateId, templateSource) = await ResolveTemplateIdFromLocationChainAsync(
                    locationNodeId,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        // 6. Load template design if resolved
        TemplateDesignSnapshot? resolvedTemplateDesign = null;
        if (!string.IsNullOrWhiteSpace(resolvedTemplateId))
        {
            resolvedTemplateDesign = await templateDesignRepository
                .GetByTemplateIdAsync(tenantId, resolvedTemplateId, cancellationToken)
                .ConfigureAwait(false);
        }

        return new ResolvedTemplateContext
        {
            LocationNodeId = locationNodeId,
            ResolvedTemplateId = resolvedTemplateId,
            TemplateSource = templateSource,
            ResolvedTemplateDesign = resolvedTemplateDesign,
            EffectiveSettings = effectiveSettings,
            InheritanceChain = chain,
        };
    }

    private static Dictionary<string, ScopedSetting> ParseSettingsJson(string settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson) || settingsJson == "{}")
        {
            return new Dictionary<string, ScopedSetting>(StringComparer.Ordinal);
        }

        try
        {
            JsonNode? root = JsonNode.Parse(settingsJson);
            if (root is not JsonObject obj)
            {
                return new Dictionary<string, ScopedSetting>(StringComparer.Ordinal);
            }

            Dictionary<string, ScopedSetting> result = new(StringComparer.Ordinal);
            foreach ((string key, JsonNode? value) in obj)
            {
                if (value is JsonObject settingObj)
                {
                    string mode = settingObj["mode"]?.GetValue<string>() ?? "inherit";
                    JsonNode? valNode = settingObj["value"];
                    object? val = valNode switch
                    {
                        JsonValue v when v.TryGetValue<string>(out string? s) => s,
                        JsonValue v when v.TryGetValue<int>(out int i) => i,
                        JsonValue v when v.TryGetValue<bool>(out bool b) => b,
                        null => null,
                        _ => valNode.ToJsonString(),
                    };
                    result[key] = new ScopedSetting(mode, val);
                }
            }

            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, ScopedSetting>(StringComparer.Ordinal);
        }
    }

    private static void ApplySettings(
        Dictionary<string, ScopedSetting> scopeSettings,
        Dictionary<string, EffectiveSettingValue> effective,
        string scopeType,
        string scopeKey)
    {
        foreach ((string key, ScopedSetting setting) in scopeSettings)
        {
            if (setting.Mode == "override")
            {
                effective[key] = new EffectiveSettingValue(key, setting.Value, scopeType, scopeKey, "override");
            }
            else if (setting.Mode == "ignore")
            {
                effective.Remove(key);
            }
            else
            {
                // inherit (default): only set if not already present
                if (!effective.ContainsKey(key))
                {
                    effective[key] = new EffectiveSettingValue(key, setting.Value, scopeType, scopeKey, "inherit");
                }
            }
        }
    }

    private async ValueTask<(string? TemplateId, string Source)> ResolveTemplateIdFromLocationChainAsync(
        string locationNodeId,
        CancellationToken cancellationToken)
    {
        HashSet<string> visited = new(StringComparer.Ordinal);
        int depth = 0;
        string? currentId = locationNodeId;
        const int maxDepth = 32;

        while (!string.IsNullOrWhiteSpace(currentId))
        {
            if (!visited.Add(currentId))
            {
                break;
            }

            LocationNodeSnapshot? node = await locationNodeReadRepository
                .GetByIdAsync(currentId, cancellationToken)
                .ConfigureAwait(false);

            if (node is null)
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(node.TemplateId))
            {
                return (node.TemplateId, depth == 0 ? "Location" : "Ancestor");
            }

            if (string.IsNullOrWhiteSpace(node.ParentLocationNodeId) || depth >= maxDepth)
            {
                break;
            }

            depth++;
            currentId = node.ParentLocationNodeId;
        }

        return (null, "None");
    }
}
