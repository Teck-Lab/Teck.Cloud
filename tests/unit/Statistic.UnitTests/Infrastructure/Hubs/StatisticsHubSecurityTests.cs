// <copyright file="StatisticsHubSecurityTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Statistic.Infrastructure.Hubs;

namespace Statistic.UnitTests.Infrastructure.Hubs;

public sealed class StatisticsHubSecurityTests
{
    [Fact]
    public void StatisticsHub_ShouldHave_AuthorizeAttribute()
    {
        // Arrange
        Type hubType = typeof(StatisticsHub);

        // Act
        AuthorizeAttribute? authorizeAttribute = hubType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        // Assert
        Assert.NotNull(authorizeAttribute);
    }

    [Fact]
    public void StatisticsHub_ShouldInheritFrom_Hub()
    {
        // Arrange
        Type hubType = typeof(StatisticsHub);

        // Act & Assert
        Assert.True(typeof(Hub).IsAssignableFrom(hubType));
    }

    [Fact]
    public void JoinDashboard_ShouldNotHave_AllowAnonymousAttribute()
    {
        // Arrange
        var methodInfo = typeof(StatisticsHub).GetMethod("JoinDashboard");
        Assert.NotNull(methodInfo);

        // Act
        var allowAnonymous = methodInfo.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true);

        // Assert
        Assert.Empty(allowAnonymous);
    }

    [Fact]
    public void JoinLocation_ShouldNotHave_AllowAnonymousAttribute()
    {
        // Arrange
        var methodInfo = typeof(StatisticsHub).GetMethod("JoinLocation");
        Assert.NotNull(methodInfo);

        // Act
        var allowAnonymous = methodInfo.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true);

        // Assert
        Assert.Empty(allowAnonymous);
    }
}
