using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using Xunit;

namespace SharedKernel.Core.UnitTests;

/// <summary>Checks tenant selection without connecting to a database.</summary>
public sealed class TenantConnectionTests
{
    /// <summary>Read and write connections are resolved independently for each tenant.</summary>
    [Fact]
    public void ConnectionsAreSeparatedByTenantAndAccess()
    {
        var fixture = new Fixture().Customize(new AutoNSubstituteCustomization());
        var config = fixture.Create<IConfiguration>();
        _ = config["ConnectionStrings:Tenants:alpha:Read"].Returns("alpha-read");
        _ = config["ConnectionStrings:Tenants:alpha:Write"].Returns("alpha-write");
        _ = config["ConnectionStrings:Tenants:beta:Read"].Returns("beta-read");
        TenantConnectionProvider.GetTenantConnection(config, "alpha", readOnly: true).ShouldBe("alpha-read");
        TenantConnectionProvider.GetTenantConnection(config, "alpha").ShouldBe("alpha-write");
        TenantConnectionProvider.GetTenantConnection(config, "beta", readOnly: true).ShouldBe("beta-read");
        _ = Should.Throw<InvalidOperationException>(() => TenantConnectionProvider.GetTenantConnection(config, Guid.NewGuid().ToString("N")));
    }

    /// <summary>A tenant cannot select a nested configuration path.</summary>
    /// <param name="tenantId">The invalid identifier.</param>
    [Theory]
    [InlineData("alpha:Write")]
    [InlineData("alpha__Write")]
    [InlineData("")]
    public void InvalidTenantIdentifiersAreRejected(string tenantId) =>
        Should.Throw<ArgumentException>(() => TenantConnectionProvider.GetTenantConnection(Substitute.For<IConfiguration>(), tenantId));
}
