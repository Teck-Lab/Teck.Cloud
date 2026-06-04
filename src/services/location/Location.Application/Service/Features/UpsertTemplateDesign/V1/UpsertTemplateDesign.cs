// <copyright file="UpsertTemplateDesign.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using Location.Application.Service.Abstractions;
using SharedKernel.Core.CQRS;

namespace Location.Application.Service.Features.UpsertTemplateDesign.V1;

/// <summary>
/// Command for creating or updating template design settings.
/// </summary>
/// <param name="TemplateId">The template identifier.</param>
/// <param name="Name">The template name.</param>
/// <param name="Width">The template width.</param>
/// <param name="Height">The template height.</param>
/// <param name="BackgroundColor">The template background color.</param>
/// <param name="ElementsJson">Serialized element payload.</param>
/// <param name="DefaultsJson">Serialized defaults payload.</param>
public sealed record UpsertTemplateDesignCommand(
    string TemplateId,
    string Name,
    int Width,
    int Height,
    string BackgroundColor,
    string ElementsJson,
    string DefaultsJson)
    : ICommand<ErrorOr<UpsertTemplateDesignResponse>>;

/// <summary>
/// Handler for <see cref="UpsertTemplateDesignCommand"/>.
/// </summary>
/// <param name="writeRepository">Template design write repository dependency.</param>
public sealed class UpsertTemplateDesignCommandHandler(ITemplateDesignWriteRepository writeRepository)
    : ICommandHandler<UpsertTemplateDesignCommand, ErrorOr<UpsertTemplateDesignResponse>>
{
    private readonly ITemplateDesignWriteRepository writeRepository = writeRepository;

    /// <summary>
    /// Creates or updates template design settings.
    /// </summary>
    /// <param name="request">The upsert template design command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The upsert template design response.</returns>
    public async ValueTask<ErrorOr<UpsertTemplateDesignResponse>> Handle(
        UpsertTemplateDesignCommand request,
        CancellationToken cancellationToken)
    {
        // TenantId will be resolved from context in a real implementation
        var snapshot = new TemplateDesignSnapshot(
            TenantId: "_current",
            request.TemplateId,
            request.Name,
            request.Width,
            request.Height,
            request.BackgroundColor,
            request.ElementsJson,
            request.DefaultsJson);

        await this.writeRepository.UpsertAsync(snapshot, cancellationToken).ConfigureAwait(false);

        return new UpsertTemplateDesignResponse
        {
            TemplateId = request.TemplateId,
            Name = request.Name,
        };
    }
}

/// <summary>
/// Response payload for template design upsert.
/// </summary>
public sealed record UpsertTemplateDesignResponse
{
    /// <summary>Gets the template identifier.</summary>
    public string TemplateId { get; init; } = string.Empty;

    /// <summary>Gets the template name.</summary>
    public string Name { get; init; } = string.Empty;
}
