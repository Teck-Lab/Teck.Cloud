// <copyright file="HandlePlanUpgradePaymentSucceededCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Tenants.Features.HandlePlanUpgradePaymentSucceeded.V1;

/// <summary>
/// Command dispatched when the Billing service confirms plan upgrade payment succeeded.
/// Applies the plan change to the tenant and re-issues the license, superseding the previous one.
/// </summary>
/// <param name="CorrelationId">The saga correlation identifier.</param>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="TargetPlan">The new plan name to apply.</param>
/// <param name="ChargeId">The external charge identifier from the payment provider.</param>
public sealed record HandlePlanUpgradePaymentSucceededCommand(
    Guid CorrelationId,
    Guid TenantId,
    string TargetPlan,
    string ChargeId)
    : ICommand<ErrorOr<Success>>;
