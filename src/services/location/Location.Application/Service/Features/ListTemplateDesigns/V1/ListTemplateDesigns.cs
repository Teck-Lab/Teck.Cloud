// <copyright file="ListTemplateDesigns.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using Location.Application.Service.Abstractions;
using SharedKernel.Core.CQRS;

namespace Location.Application.Service.Features.ListTemplateDesigns.V1;

/// <summary>
/// Query for listing template designs.
/// </summary>
public sealed record ListTemplateDesignsQuery
    : IQuery<ErrorOr<ListTemplateDesignsResponse>>;

/// <summary>
/// Handler for <see cref="ListTemplateDesignsQuery"/>.
/// </summary>
/// <param name="readRepository">Template design read repository dependency.</param>
public sealed class ListTemplateDesignsQueryHandler(ITemplateDesignReadRepository readRepository)
    : IQueryHandler<ListTemplateDesignsQuery, ErrorOr<ListTemplateDesignsResponse>>
{
    private readonly ITemplateDesignReadRepository readRepository = readRepository;

    /// <summary>
    /// Lists template designs and maps them to response items.
    /// </summary>
    /// <param name="request">The list template designs query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The template design list response.</returns>
    public async ValueTask<ErrorOr<ListTemplateDesignsResponse>> Handle(
        ListTemplateDesignsQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TemplateDesignSnapshot> designs = await this.readRepository
            .ListByTenantAsync("_current", cancellationToken)
            .ConfigureAwait(false);

        return new ListTemplateDesignsResponse
        {
            Designs = designs.Select(design => new TemplateDesignListItem
            {
                TemplateId = design.TemplateId,
                Name = design.Name,
                Width = design.Width,
                Height = design.Height,
                BackgroundColor = design.BackgroundColor,
            }).ToList(),
        };
    }
}

/// <summary>
/// Response payload for template design listing.
/// </summary>
public sealed record ListTemplateDesignsResponse
{
    /// <summary>Gets the template designs.</summary>
    public IReadOnlyList<TemplateDesignListItem> Designs { get; init; } = [];
}

/// <summary>
/// Response item for a template design.
/// </summary>
public sealed record TemplateDesignListItem
{
    /// <summary>Gets the template identifier.</summary>
    public string TemplateId { get; init; } = string.Empty;

    /// <summary>Gets the template name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the template width.</summary>
    public int Width { get; init; }

    /// <summary>Gets the template height.</summary>
    public int Height { get; init; }

    /// <summary>Gets the background color.</summary>
    public string BackgroundColor { get; init; } = string.Empty;
}
