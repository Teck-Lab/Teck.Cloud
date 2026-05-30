// <copyright file="GetPaginatedBillingTransactionsValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Billing.Application.Billing.Features.GetPaginatedBillingTransactions.V1;

/// <summary>
/// Validator for <see cref="GetPaginatedBillingTransactionsRequest"/>.
/// </summary>
public sealed class GetPaginatedBillingTransactionsValidator : AbstractValidator<GetPaginatedBillingTransactionsRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetPaginatedBillingTransactionsValidator"/> class.
    /// </summary>
    public GetPaginatedBillingTransactionsValidator()
    {
        this.RuleFor(request => request.Page).GreaterThanOrEqualTo(1);
        this.RuleFor(request => request.Size).InclusiveBetween(1, 100);
    }
}
