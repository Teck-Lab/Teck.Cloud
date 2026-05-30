// <copyright file="GetProductReadinessSummaryValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Product.Application.Service.Features.GetProductReadinessSummary.V1;

public sealed class GetProductReadinessSummaryValidator : AbstractValidator<GetProductReadinessSummaryRequest>
{
    public GetProductReadinessSummaryValidator()
    {
        RuleFor(request => request)
            .NotNull();
    }
}
