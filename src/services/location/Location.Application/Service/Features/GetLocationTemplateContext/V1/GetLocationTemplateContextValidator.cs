// <copyright file="GetLocationTemplateContextValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Location.Application.Service.Features.GetLocationTemplateContext.V1;

/// <summary>
/// Validator for <see cref="GetLocationTemplateContextRequest"/>.
/// </summary>
public sealed class GetLocationTemplateContextValidator : AbstractValidator<GetLocationTemplateContextRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetLocationTemplateContextValidator"/> class.
    /// </summary>
    public GetLocationTemplateContextValidator()
    {
        RuleFor(request => request.LocationNodeId)
            .NotEmpty();
    }
}
