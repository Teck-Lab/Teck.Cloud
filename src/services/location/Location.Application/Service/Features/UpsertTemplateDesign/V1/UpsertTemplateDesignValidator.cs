// <copyright file="UpsertTemplateDesignValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Location.Application.Service.Features.UpsertTemplateDesign.V1;

public sealed class UpsertTemplateDesignValidator : AbstractValidator<UpsertTemplateDesignCommand>
{
    public UpsertTemplateDesignValidator()
    {
        RuleFor(command => command.TemplateId)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("TemplateId is required and must not exceed 200 characters.");

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Name is required and must not exceed 200 characters.");

        RuleFor(command => command.Width)
            .GreaterThan(0)
            .WithMessage("Width must be greater than 0.");

        RuleFor(command => command.Height)
            .GreaterThan(0)
            .WithMessage("Height must be greater than 0.");

        RuleFor(command => command.BackgroundColor)
            .NotEmpty()
            .MaximumLength(32)
            .WithMessage("BackgroundColor is required and must not exceed 32 characters.");

        RuleFor(command => command.ElementsJson)
            .NotEmpty()
            .WithMessage("ElementsJson is required.");
    }
}
