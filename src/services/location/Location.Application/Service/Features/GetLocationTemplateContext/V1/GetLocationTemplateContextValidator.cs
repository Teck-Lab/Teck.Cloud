// <copyright file="GetLocationTemplateContextValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Location.Application.Service.Features.GetLocationTemplateContext.V1;

public sealed class GetLocationTemplateContextValidator : AbstractValidator<GetLocationTemplateContextRequest>
{
    public GetLocationTemplateContextValidator()
    {
        RuleFor(request => request.LocationNodeId)
            .NotEmpty();
    }
}
