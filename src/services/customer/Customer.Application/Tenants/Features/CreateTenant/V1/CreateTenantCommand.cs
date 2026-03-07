// <copyright file="CreateTenantCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Tenants.Responses;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Tenants.Features.CreateTenant.V1;

/// <summary>
/// Command to create a new tenant.
/// </summary>
/// <param name="Identifier">The tenant identifier (unique name/slug).</param>
/// <param name="Profile">The tenant profile details.</param>
/// <param name="Database">The database configuration.</param>
public sealed record CreateTenantCommand(
    string Identifier,
    TenantProfile Profile,
    TenantDatabaseSelection Database)
    : ICommand<ErrorOr<TenantResponse>>;
