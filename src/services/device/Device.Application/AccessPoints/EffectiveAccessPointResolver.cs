// <copyright file="EffectiveAccessPointResolver.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceDefinitions.Abstractions;
using Device.Application.Displays.Abstractions;
using Device.Domain.AccessPoints;
using Device.Domain.Entities.DisplayAggregate;
using DomainAccessPointReadRepository = Device.Domain.AccessPoints.IAccessPointReadRepository;

namespace Device.Application.AccessPoints;

/// <summary>
/// Resolved access point candidate for an ESL dispatch.
/// </summary>
/// <param name="Provider">ESL provider key used for vendor-worker routing.</param>
/// <param name="AccessPoint">The resolved access point, if one is available.</param>
public sealed record EffectiveAccessPoint(string Provider, AccessPoint? AccessPoint);

/// <summary>
/// Resolves the effective access point for a display based on its location and device definition provider.
/// </summary>
public sealed class EffectiveAccessPointResolver(
    IDisplayWriteRepository displayWriteRepository,
    IDeviceDefinitionReadRepository deviceDefinitionReadRepository,
    DomainAccessPointReadRepository accessPointReadRepository,
    ILocationNodeResolver locationNodeResolver)
{
    private const string StubProvider = "Stub";
    private const int MaxAncestorDepth = 32;

    private readonly IDisplayWriteRepository displayWriteRepository = displayWriteRepository;
    private readonly IDeviceDefinitionReadRepository deviceDefinitionReadRepository = deviceDefinitionReadRepository;
    private readonly DomainAccessPointReadRepository accessPointReadRepository = accessPointReadRepository;
    private readonly ILocationNodeResolver locationNodeResolver = locationNodeResolver;

    /// <summary>
    /// Resolves the first online access point with available capacity for the display location.
    /// </summary>
    /// <param name="displayId">Display identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved provider and access point, or <see langword="null"/> when the display does not exist.</returns>
    public async ValueTask<EffectiveAccessPoint?> ResolveAsync(Guid displayId, CancellationToken cancellationToken)
    {
        Display? display = await this.displayWriteRepository
            .FindByIdAsync(displayId, cancellationToken)
            .ConfigureAwait(false);

        if (display is null)
        {
            return null;
        }

        string provider = await ResolveProviderAsync(display, cancellationToken).ConfigureAwait(false);

        AccessPoint? accessPoint = await TryResolveAccessPointInChainAsync(provider, display.LocationNodeId, cancellationToken)
            .ConfigureAwait(false);

        if (accessPoint is null)
        {
            return new EffectiveAccessPoint(provider, AccessPoint: null);
        }

        return new EffectiveAccessPoint(provider, accessPoint);
    }

    private async ValueTask<AccessPoint?> TryResolveAccessPointInChainAsync(
        string provider,
        string ownLocationNodeId,
        CancellationToken cancellationToken)
    {
        AccessPoint? ownLocationAccessPoint = await FindAvailableAccessPointAsync(provider, ownLocationNodeId, cancellationToken)
            .ConfigureAwait(false);

        if (ownLocationAccessPoint is not null)
        {
            return ownLocationAccessPoint;
        }

        IReadOnlyList<string> ancestors = await this.locationNodeResolver
            .GetAncestorChainAsync(ownLocationNodeId, cancellationToken)
            .ConfigureAwait(false);

        foreach (string ancestorLocationNodeId in ancestors.Take(MaxAncestorDepth))
        {
            AccessPoint? ancestorAccessPoint = await FindAvailableAccessPointAsync(provider, ancestorLocationNodeId, cancellationToken)
                .ConfigureAwait(false);

            if (ancestorAccessPoint is not null)
            {
                return ancestorAccessPoint;
            }
        }

        return null;
    }

    private async ValueTask<AccessPoint?> FindAvailableAccessPointAsync(
        string provider,
        string locationNodeId,
        CancellationToken cancellationToken)
    {
        AccessPoint? accessPoint = await this.accessPointReadRepository
            .FindByVendorAndLocationAsync(provider, locationNodeId, cancellationToken)
            .ConfigureAwait(false);

        if (accessPoint is null || accessPoint.Status != AccessPointStatus.Online || accessPoint.CurrentLoad >= accessPoint.MaxCapacity)
        {
            return null;
        }

        return accessPoint;
    }

    private async ValueTask<string> ResolveProviderAsync(Display display, CancellationToken cancellationToken)
    {
        if (display.DeviceDefinitionId is null)
        {
            return StubProvider;
        }

        DeviceDefinitionSnapshot? deviceDefinition = await this.deviceDefinitionReadRepository
            .GetByIdAsync(display.DeviceDefinitionId.Value, cancellationToken)
            .ConfigureAwait(false);

        return deviceDefinition?.EslProvider.Name ?? StubProvider;
    }
}
