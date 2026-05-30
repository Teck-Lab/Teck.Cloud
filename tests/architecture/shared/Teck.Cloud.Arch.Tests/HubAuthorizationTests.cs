// <copyright file="HubAuthorizationTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Reflection;
using Xunit;

namespace Teck.Cloud.Arch.Tests;

/// <summary>
/// Architecture tests verifying SignalR hubs are properly secured.
/// </summary>
public sealed class HubAuthorizationTests
{
    [Fact]
    public void AllSignalRHubs_ShouldHave_AuthorizeAttribute()
    {
        // Load all assemblies that reference SignalR
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.GetReferencedAssemblies().Any(r => r.Name == "Microsoft.AspNetCore.SignalR.Core"))
            .ToArray();

        // Find all Hub types
        var hubTypes = assemblies
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.OfType<Type>();
                }
            })
            .Where(t => t is not null && typeof(Hub).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
            .ToList();

        // Assert each hub has [Authorize]
        var unauthorizedHubs = new List<string>();
        foreach (Type hubType in hubTypes)
        {
            bool hasAuthorize = hubType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Any();
            if (!hasAuthorize)
            {
                unauthorizedHubs.Add(hubType.FullName ?? hubType.Name);
            }
        }

        Assert.Empty(unauthorizedHubs);
    }
}
