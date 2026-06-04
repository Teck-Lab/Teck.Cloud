// <copyright file="GetProductReadinessSummaryValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Product.Application.Service.Features.GetProductReadinessSummary.V1;

/// <summary>
/// Validates <see cref="GetProductReadinessSummaryRequest"/> instances.
/// </summary>
public sealed class GetProductReadinessSummaryValidator : AbstractValidator<GetProductReadinessSummaryRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetProductReadinessSummaryValidator"/> class.
    /// </summary>
    public GetProductReadinessSummaryValidator()
    {
        RuleFor(request => request)
            .NotNull();
    }
}
