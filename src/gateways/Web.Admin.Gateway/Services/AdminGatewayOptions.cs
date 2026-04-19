// <copyright file="AdminGatewayOptions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Web.Admin.Gateway.Services;

internal sealed record AdminGatewayOptions(string PlatformAdminRole);

internal static class AdminGatewayOptionsExtensions
{
    public static AdminGatewayOptions GetAdminGatewayOptions(this IConfiguration configuration)
    {
        return new AdminGatewayOptions(
            PlatformAdminRole: configuration["AdminGateway:PlatformAdminRole"] ?? "platform-admin");
    }
}
