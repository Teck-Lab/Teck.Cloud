// <copyright file="GetResolvedTemplateValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Location.Application.Service.Features.GetResolvedTemplate.V1;

public sealed class GetResolvedTemplateValidator : AbstractValidator<GetResolvedTemplateQuery>
{
    public GetResolvedTemplateValidator()
    {
        RuleFor(query => query.LocationNodeId)
            .NotEmpty()
            .WithMessage("LocationNodeId is required.");
    }
}
