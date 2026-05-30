// <copyright file="TemplateInheritanceResolverTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Location.Application.Service.Abstractions;
using Location.Infrastructure.Service;
using NSubstitute;
using Shouldly;

namespace Location.UnitTests.Infrastructure.Service;

public class TemplateInheritanceResolverTests
{
    private readonly ITemplateScopeSettingsReadRepository _scopeSettingsRepository;
    private readonly ITemplateDesignReadRepository _templateDesignRepository;
    private readonly ILocationNodeReadRepository _locationNodeReadRepository;
    private readonly ILocationGroupReadRepository _locationGroupReadRepository;
    private readonly TemplateInheritanceResolver _sut;

    public TemplateInheritanceResolverTests()
    {
        _scopeSettingsRepository = Substitute.For<ITemplateScopeSettingsReadRepository>();
        _templateDesignRepository = Substitute.For<ITemplateDesignReadRepository>();
        _locationNodeReadRepository = Substitute.For<ILocationNodeReadRepository>();
        _locationGroupReadRepository = Substitute.For<ILocationGroupReadRepository>();

        _sut = new TemplateInheritanceResolver(
            _scopeSettingsRepository,
            _templateDesignRepository,
            _locationNodeReadRepository,
            _locationGroupReadRepository);
    }

    [Fact]
    public async Task ResolveAsync_WithExplicitTemplateId_ShouldReturnRequestSource()
    {
        // Arrange
        string tenantId = "tenant-1";
        string locationNodeId = "loc-1";
        string explicitTemplateId = "tmpl-explicit";

        _locationNodeReadRepository.GetByIdAsync(locationNodeId, Arg.Any<CancellationToken>())
            .Returns(new LocationNodeSnapshot(locationNodeId, null, null, "Store A", null, null, null));

        _templateDesignRepository.GetByTemplateIdAsync(tenantId, explicitTemplateId, Arg.Any<CancellationToken>())
            .Returns(new TemplateDesignSnapshot(tenantId, explicitTemplateId, "Test", 100, 100, "#FFF", "[]", "{}"));

        // Act
        ResolvedTemplateContext result = await _sut.ResolveAsync(tenantId, locationNodeId, explicitTemplateId, CancellationToken.None);

        // Assert
        result.ResolvedTemplateId.ShouldBe(explicitTemplateId);
        result.TemplateSource.ShouldBe("Request");
        result.InheritanceChain.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ResolveAsync_WithTenantSettings_ShouldApplyInheritMode()
    {
        // Arrange
        string tenantId = "tenant-1";
        string locationNodeId = "loc-1";

        _scopeSettingsRepository.GetByScopeAsync(tenantId, "Tenant", "_tenant", Arg.Any<CancellationToken>())
            .Returns(new TemplateScopeSettingsSnapshot(tenantId, "Tenant", "_tenant",
                """{"logo":{"mode":"inherit","value":"tenant-logo.png"}}"""));

        _locationNodeReadRepository.GetByIdAsync(locationNodeId, Arg.Any<CancellationToken>())
            .Returns(new LocationNodeSnapshot(locationNodeId, null, "tmpl-1", "Store A", null, null, null));

        _templateDesignRepository.GetByTemplateIdAsync(tenantId, "tmpl-1", Arg.Any<CancellationToken>())
            .Returns(new TemplateDesignSnapshot(tenantId, "tmpl-1", "Test", 100, 100, "#FFF", "[]", "{}"));

        // Act
        ResolvedTemplateContext result = await _sut.ResolveAsync(tenantId, locationNodeId, null, CancellationToken.None);

        // Assert
        result.EffectiveSettings.ShouldContainKey("logo");
        result.EffectiveSettings["logo"].Value.ShouldBe("tenant-logo.png");
        result.EffectiveSettings["logo"].SourceScopeType.ShouldBe("Tenant");
        result.InheritanceChain.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ResolveAsync_WithOverrideMode_ShouldOverrideTenantValue()
    {
        // Arrange
        string tenantId = "tenant-1";
        string locationNodeId = "loc-1";

        _scopeSettingsRepository.GetByScopeAsync(tenantId, "Tenant", "_tenant", Arg.Any<CancellationToken>())
            .Returns(new TemplateScopeSettingsSnapshot(tenantId, "Tenant", "_tenant",
                """{"logo":{"mode":"inherit","value":"tenant-logo.png"}}"""));

        _scopeSettingsRepository.GetByScopeAsync(tenantId, "Location", locationNodeId, Arg.Any<CancellationToken>())
            .Returns(new TemplateScopeSettingsSnapshot(tenantId, "Location", locationNodeId,
                """{"logo":{"mode":"override","value":"location-logo.png"}}"""));

        _locationNodeReadRepository.GetByIdAsync(locationNodeId, Arg.Any<CancellationToken>())
            .Returns(new LocationNodeSnapshot(locationNodeId, null, "tmpl-1", "Store A", null, null, null));

        _templateDesignRepository.GetByTemplateIdAsync(tenantId, "tmpl-1", Arg.Any<CancellationToken>())
            .Returns(new TemplateDesignSnapshot(tenantId, "tmpl-1", "Test", 100, 100, "#FFF", "[]", "{}"));

        // Act
        ResolvedTemplateContext result = await _sut.ResolveAsync(tenantId, locationNodeId, null, CancellationToken.None);

        // Assert
        result.EffectiveSettings["logo"].Value.ShouldBe("location-logo.png");
        result.EffectiveSettings["logo"].SourceScopeType.ShouldBe("Location");
        result.EffectiveSettings["logo"].Mode.ShouldBe("override");
    }

    [Fact]
    public async Task ResolveAsync_WithIgnoreMode_ShouldRemoveEffectiveSetting()
    {
        // Arrange
        string tenantId = "tenant-1";
        string locationNodeId = "loc-1";

        _scopeSettingsRepository.GetByScopeAsync(tenantId, "Tenant", "_tenant", Arg.Any<CancellationToken>())
            .Returns(new TemplateScopeSettingsSnapshot(tenantId, "Tenant", "_tenant",
                """{"logo":{"mode":"inherit","value":"tenant-logo.png"}}"""));

        _scopeSettingsRepository.GetByScopeAsync(tenantId, "Location", locationNodeId, Arg.Any<CancellationToken>())
            .Returns(new TemplateScopeSettingsSnapshot(tenantId, "Location", locationNodeId,
                """{"logo":{"mode":"ignore","value":null}}"""));

        _locationNodeReadRepository.GetByIdAsync(locationNodeId, Arg.Any<CancellationToken>())
            .Returns(new LocationNodeSnapshot(locationNodeId, null, "tmpl-1", "Store A", null, null, null));

        _templateDesignRepository.GetByTemplateIdAsync(tenantId, "tmpl-1", Arg.Any<CancellationToken>())
            .Returns(new TemplateDesignSnapshot(tenantId, "tmpl-1", "Test", 100, 100, "#FFF", "[]", "{}"));

        // Act
        ResolvedTemplateContext result = await _sut.ResolveAsync(tenantId, locationNodeId, null, CancellationToken.None);

        // Assert
        result.EffectiveSettings.ShouldNotContainKey("logo");
    }

    [Fact]
    public async Task ResolveAsync_WithLocationGroup_ShouldIncludeGroupInChain()
    {
        // Arrange
        string tenantId = "tenant-1";
        string locationNodeId = "loc-1";
        string groupId = "group-1";

        _scopeSettingsRepository.GetByScopeAsync(tenantId, "Tenant", "_tenant", Arg.Any<CancellationToken>())
            .Returns(new TemplateScopeSettingsSnapshot(tenantId, "Tenant", "_tenant",
                """{"logo":{"mode":"inherit","value":"tenant-logo.png"}}"""));

        _scopeSettingsRepository.GetByScopeAsync(tenantId, "LocationGroup", groupId, Arg.Any<CancellationToken>())
            .Returns(new TemplateScopeSettingsSnapshot(tenantId, "LocationGroup", groupId,
                """{"color":{"mode":"inherit","value":"#FF0000"}}"""));

        _locationNodeReadRepository.GetByIdAsync(locationNodeId, Arg.Any<CancellationToken>())
            .Returns(new LocationNodeSnapshot(locationNodeId, null, "tmpl-1", "Store A", groupId, null, null));

        _locationGroupReadRepository.GetByIdAsync(tenantId, groupId, Arg.Any<CancellationToken>())
            .Returns(new LocationGroupSnapshot(tenantId, groupId, "North Region"));

        _templateDesignRepository.GetByTemplateIdAsync(tenantId, "tmpl-1", Arg.Any<CancellationToken>())
            .Returns(new TemplateDesignSnapshot(tenantId, "tmpl-1", "Test", 100, 100, "#FFF", "[]", "{}"));

        // Act
        ResolvedTemplateContext result = await _sut.ResolveAsync(tenantId, locationNodeId, null, CancellationToken.None);

        // Assert
        result.InheritanceChain.Count.ShouldBe(2);
        result.InheritanceChain[0].ScopeType.ShouldBe("Tenant");
        result.InheritanceChain[1].ScopeType.ShouldBe("LocationGroup");
        result.InheritanceChain[1].ScopeName.ShouldBe("North Region");
        result.EffectiveSettings.ShouldContainKey("color");
    }

    [Fact]
    public async Task ResolveAsync_WithAncestorChain_ShouldWalkUpToMaxDepth()
    {
        // Arrange
        string tenantId = "tenant-1";
        string locationNodeId = "loc-child";

        _scopeSettingsRepository.GetByScopeAsync(tenantId, "Location", "loc-child", Arg.Any<CancellationToken>())
            .Returns(new TemplateScopeSettingsSnapshot(tenantId, "Location", "loc-child",
                """{"setting1":{"mode":"inherit","value":"child-val"}}"""));

        _scopeSettingsRepository.GetByScopeAsync(tenantId, "Location", "loc-parent", Arg.Any<CancellationToken>())
            .Returns(new TemplateScopeSettingsSnapshot(tenantId, "Location", "loc-parent",
                """{"setting1":{"mode":"inherit","value":"parent-val"}}"""));

        _scopeSettingsRepository.GetByScopeAsync(tenantId, "Location", "loc-grandparent", Arg.Any<CancellationToken>())
            .Returns(new TemplateScopeSettingsSnapshot(tenantId, "Location", "loc-grandparent",
                """{"setting1":{"mode":"inherit","value":"grandparent-val"}}"""));

        _locationNodeReadRepository.GetByIdAsync("loc-child", Arg.Any<CancellationToken>())
            .Returns(new LocationNodeSnapshot("loc-child", "loc-parent", null, "Child", null, null, null));

        _locationNodeReadRepository.GetByIdAsync("loc-parent", Arg.Any<CancellationToken>())
            .Returns(new LocationNodeSnapshot("loc-parent", "loc-grandparent", "tmpl-parent", "Parent", null, null, null));

        _locationNodeReadRepository.GetByIdAsync("loc-grandparent", Arg.Any<CancellationToken>())
            .Returns(new LocationNodeSnapshot("loc-grandparent", null, null, "Grandparent", null, null, null));

        _templateDesignRepository.GetByTemplateIdAsync(tenantId, "tmpl-parent", Arg.Any<CancellationToken>())
            .Returns(new TemplateDesignSnapshot(tenantId, "tmpl-parent", "Parent Template", 100, 100, "#FFF", "[]", "{}"));

        // Act
        ResolvedTemplateContext result = await _sut.ResolveAsync(tenantId, locationNodeId, null, CancellationToken.None);

        // Assert
        result.ResolvedTemplateId.ShouldBe("tmpl-parent");
        result.TemplateSource.ShouldBe("Ancestor");
        result.InheritanceChain.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ResolveAsync_WithCircularReference_ShouldBreakLoop()
    {
        // Arrange
        string tenantId = "tenant-1";
        string locationNodeId = "loc-a";

        _scopeSettingsRepository.GetByScopeAsync(tenantId, "Location", "loc-a", Arg.Any<CancellationToken>())
            .Returns(new TemplateScopeSettingsSnapshot(tenantId, "Location", "loc-a",
                """{"setting1":{"mode":"inherit","value":"a-val"}}"""));

        _scopeSettingsRepository.GetByScopeAsync(tenantId, "Location", "loc-b", Arg.Any<CancellationToken>())
            .Returns(new TemplateScopeSettingsSnapshot(tenantId, "Location", "loc-b",
                """{"setting1":{"mode":"inherit","value":"b-val"}}"""));

        _locationNodeReadRepository.GetByIdAsync("loc-a", Arg.Any<CancellationToken>())
            .Returns(new LocationNodeSnapshot("loc-a", "loc-b", null, "A", null, null, null));

        _locationNodeReadRepository.GetByIdAsync("loc-b", Arg.Any<CancellationToken>())
            .Returns(new LocationNodeSnapshot("loc-b", "loc-a", null, "B", null, null, null));

        // Act
        ResolvedTemplateContext result = await _sut.ResolveAsync(tenantId, locationNodeId, null, CancellationToken.None);

        // Assert
        result.InheritanceChain.Count.ShouldBe(2);
        result.ResolvedTemplateId.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_WithTemplateIdInEffectiveSettings_ShouldUseSettingValue()
    {
        // Arrange
        string tenantId = "tenant-1";
        string locationNodeId = "loc-1";

        _scopeSettingsRepository.GetByScopeAsync(tenantId, "Tenant", "_tenant", Arg.Any<CancellationToken>())
            .Returns(new TemplateScopeSettingsSnapshot(tenantId, "Tenant", "_tenant",
                """{"templateId":{"mode":"inherit","value":"tmpl-from-setting"}}"""));

        _locationNodeReadRepository.GetByIdAsync(locationNodeId, Arg.Any<CancellationToken>())
            .Returns(new LocationNodeSnapshot(locationNodeId, null, null, "Store A", null, null, null));

        _templateDesignRepository.GetByTemplateIdAsync(tenantId, "tmpl-from-setting", Arg.Any<CancellationToken>())
            .Returns(new TemplateDesignSnapshot(tenantId, "tmpl-from-setting", "Setting Template", 100, 100, "#FFF", "[]", "{}"));

        // Act
        ResolvedTemplateContext result = await _sut.ResolveAsync(tenantId, locationNodeId, null, CancellationToken.None);

        // Assert
        result.ResolvedTemplateId.ShouldBe("tmpl-from-setting");
        result.TemplateSource.ShouldBe("TenantSetting");
    }

    [Fact]
    public async Task ResolveAsync_WithNoTemplate_ShouldReturnNullTemplateId()
    {
        // Arrange
        string tenantId = "tenant-1";
        string locationNodeId = "loc-1";

        _locationNodeReadRepository.GetByIdAsync(locationNodeId, Arg.Any<CancellationToken>())
            .Returns(new LocationNodeSnapshot(locationNodeId, null, null, "Store A", null, null, null));

        // Act
        ResolvedTemplateContext result = await _sut.ResolveAsync(tenantId, locationNodeId, null, CancellationToken.None);

        // Assert
        result.ResolvedTemplateId.ShouldBeNull();
        result.TemplateSource.ShouldBe("None");
    }

    [Fact]
    public async Task ResolveAsync_WithEmptySettingsJson_ShouldNotThrow()
    {
        // Arrange
        string tenantId = "tenant-1";
        string locationNodeId = "loc-1";

        _scopeSettingsRepository.GetByScopeAsync(tenantId, "Tenant", "_tenant", Arg.Any<CancellationToken>())
            .Returns(new TemplateScopeSettingsSnapshot(tenantId, "Tenant", "_tenant", "{}"));

        _locationNodeReadRepository.GetByIdAsync(locationNodeId, Arg.Any<CancellationToken>())
            .Returns(new LocationNodeSnapshot(locationNodeId, null, "tmpl-1", "Store A", null, null, null));

        _templateDesignRepository.GetByTemplateIdAsync(tenantId, "tmpl-1", Arg.Any<CancellationToken>())
            .Returns(new TemplateDesignSnapshot(tenantId, "tmpl-1", "Test", 100, 100, "#FFF", "[]", "{}"));

        // Act
        ResolvedTemplateContext result = await _sut.ResolveAsync(tenantId, locationNodeId, null, CancellationToken.None);

        // Assert
        result.EffectiveSettings.Count.ShouldBe(0);
        result.InheritanceChain.Count.ShouldBe(1);
    }
}
