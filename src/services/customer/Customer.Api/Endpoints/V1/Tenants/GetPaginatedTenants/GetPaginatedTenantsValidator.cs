// <copyright file="GetPaginatedTenantsValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Customer.Api.Endpoints.V1.Tenants.GetPaginatedTenants;

/// <summary>
/// Validator for paginated tenant request.
/// </summary>
internal sealed class GetPaginatedTenantsValidator : AbstractValidator<GetPaginatedTenantsRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetPaginatedTenantsValidator"/> class.
    /// </summary>
    public GetPaginatedTenantsValidator()
    {
        this.RuleFor(request => request.Page)
            .NotEmpty()
            .GreaterThan(0);

        this.RuleFor(request => request.Size)
            .NotEmpty()
            .GreaterThan(0);
    }
}
