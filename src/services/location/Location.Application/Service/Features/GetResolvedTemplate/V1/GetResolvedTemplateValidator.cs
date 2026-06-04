// <copyright file="GetResolvedTemplateValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Location.Application.Service.Features.GetResolvedTemplate.V1;

/// <summary>
/// Validator for <see cref="GetResolvedTemplateQuery"/>.
/// </summary>
public sealed class GetResolvedTemplateValidator : AbstractValidator<GetResolvedTemplateQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetResolvedTemplateValidator"/> class.
    /// </summary>
    public GetResolvedTemplateValidator()
    {
        RuleFor(query => query.LocationNodeId)
            .NotEmpty()
            .WithMessage("LocationNodeId is required.");
    }
}
