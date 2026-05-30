// <copyright file="ListTemplateDesigns.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using Location.Application.Service.Abstractions;
using SharedKernel.Core.CQRS;

namespace Location.Application.Service.Features.ListTemplateDesigns.V1;

public sealed record ListTemplateDesignsQuery
    : IQuery<ErrorOr<ListTemplateDesignsResponse>>;

public sealed class ListTemplateDesignsQueryHandler(ITemplateDesignReadRepository readRepository)
    : IQueryHandler<ListTemplateDesignsQuery, ErrorOr<ListTemplateDesignsResponse>>
{
    private readonly ITemplateDesignReadRepository readRepository = readRepository;

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

public sealed record ListTemplateDesignsResponse
{
    public IReadOnlyList<TemplateDesignListItem> Designs { get; init; } = [];
}

public sealed record TemplateDesignListItem
{
    public string TemplateId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int Width { get; init; }

    public int Height { get; init; }

    public string BackgroundColor { get; init; } = string.Empty;
}
