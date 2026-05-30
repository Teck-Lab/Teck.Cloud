// <copyright file="EffectiveAccessPointResolverTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.AccessPoints;
using Device.Application.DeviceDefinitions.Abstractions;
using Device.Application.Displays.Abstractions;
using Device.Domain.AccessPoints;
using Device.Domain.Entities.DeviceDefinitionAggregate;
using Device.Domain.Entities.DisplayAggregate;
using NSubstitute;
using SharedKernel.Core.Devices;
using Shouldly;

#pragma warning disable CA2012

namespace Device.UnitTests.Application.AccessPoints;

public sealed class EffectiveAccessPointResolverTests
{
    private readonly IDisplayWriteRepository _displayWriteRepository;
    private readonly IDeviceDefinitionReadRepository _deviceDefinitionReadRepository;
    private readonly IAccessPointReadRepository _accessPointReadRepository;
    private readonly ILocationNodeResolver _locationNodeResolver;
    private readonly EffectiveAccessPointResolver _resolver;

    public EffectiveAccessPointResolverTests()
    {
        _displayWriteRepository = Substitute.For<IDisplayWriteRepository>();
        _deviceDefinitionReadRepository = Substitute.For<IDeviceDefinitionReadRepository>();
        _accessPointReadRepository = Substitute.For<IAccessPointReadRepository>();
        _locationNodeResolver = Substitute.For<ILocationNodeResolver>();

        _resolver = new EffectiveAccessPointResolver(
            _displayWriteRepository,
            _deviceDefinitionReadRepository,
            _accessPointReadRepository,
            _locationNodeResolver);
    }

    [Fact]
    public async Task ResolveAsync_WhenDisplayNotFound_ShouldReturnNull()
    {
        // Arrange
        var displayId = Guid.NewGuid();
        _displayWriteRepository.FindByIdAsync(displayId, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Display?>(null));

        // Act
        EffectiveAccessPoint? result = await _resolver.ResolveAsync(displayId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_WhenDisplayHasNoDeviceDefinition_ShouldUseStubProvider()
    {
        // Arrange
        var display = Display.Create("DISP-01", "loc-own", null).Value;
        _displayWriteRepository.FindByIdAsync(display.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Display?>(display));
        _accessPointReadRepository.FindByVendorAndLocationAsync("Stub", "loc-own", Arg.Any<CancellationToken>()).Returns(Task.FromResult<AccessPoint?>(null));
        _locationNodeResolver.GetAncestorChainAsync("loc-own", Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult<IReadOnlyList<string>>([]));

        // Act
        EffectiveAccessPoint? result = await _resolver.ResolveAsync(display.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Provider.ShouldBe("Stub");
        result.AccessPoint.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_WhenOwnLocationHasOnlineAccessPointWithCapacity_ShouldReturnOwnAccessPoint()
    {
        // Arrange
        var definitionId = Guid.NewGuid();
        var display = Display.Create("DISP-01", "loc-own", definitionId).Value;
        var snapshot = new DeviceDefinitionSnapshot(definitionId, "M1", "n", null, null, DisplayInkColor.Black, false, EslProvider.Hanshow, null, null, null);
        AccessPoint own = AccessPoint.Create("AP-001", "Hanshow", "loc-own", 2).Value;

        _displayWriteRepository.FindByIdAsync(display.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Display?>(display));
        _deviceDefinitionReadRepository.GetByIdAsync(definitionId, Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult<DeviceDefinitionSnapshot?>(snapshot));
        _accessPointReadRepository.FindByVendorAndLocationAsync("Hanshow", "loc-own", Arg.Any<CancellationToken>()).Returns(Task.FromResult<AccessPoint?>(own));

        // Act
        EffectiveAccessPoint? result = await _resolver.ResolveAsync(display.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Provider.ShouldBe("Hanshow");
        result.AccessPoint.ShouldBe(own);
    }

    [Fact]
    public async Task ResolveAsync_WhenOwnLocationAccessPointIsOffline_ShouldWalkAncestorsAndReturnParentAccessPoint()
    {
        // Arrange
        var definitionId = Guid.NewGuid();
        var display = Display.Create("DISP-01", "loc-own", definitionId).Value;
        var snapshot = new DeviceDefinitionSnapshot(definitionId, "M1", "n", null, null, DisplayInkColor.Black, false, EslProvider.Hanshow, null, null, null);

        AccessPoint own = AccessPoint.Create("AP-001", "Hanshow", "loc-own", 2).Value;
        own.SetStatus(AccessPointStatus.Offline);
        AccessPoint parent = AccessPoint.Create("AP-002", "Hanshow", "loc-parent", 2).Value;

        _displayWriteRepository.FindByIdAsync(display.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Display?>(display));
        _deviceDefinitionReadRepository.GetByIdAsync(definitionId, Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult<DeviceDefinitionSnapshot?>(snapshot));
        _accessPointReadRepository.FindByVendorAndLocationAsync("Hanshow", "loc-own", Arg.Any<CancellationToken>()).Returns(Task.FromResult<AccessPoint?>(own));
        _locationNodeResolver.GetAncestorChainAsync("loc-own", Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult<IReadOnlyList<string>>(["loc-parent"]));
        _accessPointReadRepository.FindByVendorAndLocationAsync("Hanshow", "loc-parent", Arg.Any<CancellationToken>()).Returns(Task.FromResult<AccessPoint?>(parent));

        // Act
        EffectiveAccessPoint? result = await _resolver.ResolveAsync(display.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.AccessPoint.ShouldBe(parent);
    }

    [Fact]
    public async Task ResolveAsync_WhenOwnLocationAccessPointAtCapacity_ShouldWalkAncestorsAndReturnGrandparentAccessPoint()
    {
        // Arrange
        var definitionId = Guid.NewGuid();
        var display = Display.Create("DISP-01", "loc-own", definitionId).Value;
        var snapshot = new DeviceDefinitionSnapshot(definitionId, "M1", "n", null, null, DisplayInkColor.Black, false, EslProvider.Hanshow, null, null, null);

        AccessPoint own = AccessPoint.Create("AP-001", "Hanshow", "loc-own", 1).Value;
        own.IncrementLoad();
        AccessPoint grandparent = AccessPoint.Create("AP-003", "Hanshow", "loc-grand", 2).Value;

        _displayWriteRepository.FindByIdAsync(display.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Display?>(display));
        _deviceDefinitionReadRepository.GetByIdAsync(definitionId, Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult<DeviceDefinitionSnapshot?>(snapshot));
        _accessPointReadRepository.FindByVendorAndLocationAsync("Hanshow", "loc-own", Arg.Any<CancellationToken>()).Returns(Task.FromResult<AccessPoint?>(own));
        _locationNodeResolver.GetAncestorChainAsync("loc-own", Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult<IReadOnlyList<string>>(["loc-parent", "loc-grand"]));
        _accessPointReadRepository.FindByVendorAndLocationAsync("Hanshow", "loc-parent", Arg.Any<CancellationToken>()).Returns(Task.FromResult<AccessPoint?>(null));
        _accessPointReadRepository.FindByVendorAndLocationAsync("Hanshow", "loc-grand", Arg.Any<CancellationToken>()).Returns(Task.FromResult<AccessPoint?>(grandparent));

        // Act
        EffectiveAccessPoint? result = await _resolver.ResolveAsync(display.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.AccessPoint.ShouldBe(grandparent);
    }

    [Fact]
    public async Task ResolveAsync_WhenNoAccessPointExistsInChain_ShouldReturnProviderWithNullAccessPoint()
    {
        // Arrange
        var definitionId = Guid.NewGuid();
        var display = Display.Create("DISP-01", "loc-own", definitionId).Value;
        var snapshot = new DeviceDefinitionSnapshot(definitionId, "M1", "n", null, null, DisplayInkColor.Black, false, EslProvider.Hanshow, null, null, null);

        _displayWriteRepository.FindByIdAsync(display.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Display?>(display));
        _deviceDefinitionReadRepository.GetByIdAsync(definitionId, Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult<DeviceDefinitionSnapshot?>(snapshot));
        _accessPointReadRepository.FindByVendorAndLocationAsync("Hanshow", "loc-own", Arg.Any<CancellationToken>()).Returns(Task.FromResult<AccessPoint?>(null));
        _locationNodeResolver.GetAncestorChainAsync("loc-own", Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult<IReadOnlyList<string>>(["loc-parent"]));
        _accessPointReadRepository.FindByVendorAndLocationAsync("Hanshow", "loc-parent", Arg.Any<CancellationToken>()).Returns(Task.FromResult<AccessPoint?>(null));

        // Act
        EffectiveAccessPoint? result = await _resolver.ResolveAsync(display.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Provider.ShouldBe("Hanshow");
        result.AccessPoint.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_WhenAncestorChainIsEmpty_ShouldReturnProviderWithNullAccessPoint()
    {
        // Arrange
        var definitionId = Guid.NewGuid();
        var display = Display.Create("DISP-01", "root", definitionId).Value;
        var snapshot = new DeviceDefinitionSnapshot(definitionId, "M1", "n", null, null, DisplayInkColor.Black, false, EslProvider.Hanshow, null, null, null);

        _displayWriteRepository.FindByIdAsync(display.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Display?>(display));
        _deviceDefinitionReadRepository.GetByIdAsync(definitionId, Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult<DeviceDefinitionSnapshot?>(snapshot));
        _accessPointReadRepository.FindByVendorAndLocationAsync("Hanshow", "root", Arg.Any<CancellationToken>()).Returns(Task.FromResult<AccessPoint?>(null));
        _locationNodeResolver.GetAncestorChainAsync("root", Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult<IReadOnlyList<string>>([]));

        // Act
        EffectiveAccessPoint? result = await _resolver.ResolveAsync(display.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Provider.ShouldBe("Hanshow");
        result.AccessPoint.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_WhenVendorDoesNotMatch_ShouldReturnProviderWithNullAccessPoint()
    {
        // Arrange
        var definitionId = Guid.NewGuid();
        var display = Display.Create("DISP-01", "loc-own", definitionId).Value;
        var snapshot = new DeviceDefinitionSnapshot(definitionId, "M1", "n", null, null, DisplayInkColor.Black, false, EslProvider.SoluM, null, null, null);

        _displayWriteRepository.FindByIdAsync(display.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Display?>(display));
        _deviceDefinitionReadRepository.GetByIdAsync(definitionId, Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult<DeviceDefinitionSnapshot?>(snapshot));
        _accessPointReadRepository.FindByVendorAndLocationAsync("SoluM", "loc-own", Arg.Any<CancellationToken>()).Returns(Task.FromResult<AccessPoint?>(null));
        _locationNodeResolver.GetAncestorChainAsync("loc-own", Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult<IReadOnlyList<string>>([]));

        // Act
        EffectiveAccessPoint? result = await _resolver.ResolveAsync(display.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Provider.ShouldBe("SoluM");
        result.AccessPoint.ShouldBeNull();
    }
}

#pragma warning restore CA2012
