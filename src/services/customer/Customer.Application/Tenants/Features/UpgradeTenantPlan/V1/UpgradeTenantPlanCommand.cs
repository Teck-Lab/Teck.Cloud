// <copyright file="UpgradeTenantPlanCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Tenants.Features.UpgradeTenantPlan.V1;

/// <summary>
/// Command to initiate a tenant plan upgrade by publishing an integration event
/// that triggers the Billing saga for prorated payment charging.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="TargetPlan">The plan name to upgrade to.</param>
/// <param name="Currency">The ISO 4217 currency code for the charge.</param>
public sealed record UpgradeTenantPlanCommand(
    Guid TenantId,
    string TargetPlan,
    string Currency)
    : ICommand<ErrorOr<Success>>;
