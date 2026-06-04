// <copyright file="GetDisplayModels.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using Location.Application.Service.Abstractions;
using SharedKernel.Core.CQRS;

namespace Location.Application.Service.Features.GetDisplayModels.V1;

/// <summary>
/// Query for resolving display models available to a tenant.
/// </summary>
/// <param name="TenantId">Tenant identifier from the inbound request context.</param>
public sealed record GetDisplayModelsQuery(string TenantId)
    : IQuery<ErrorOr<GetDisplayModelsResponse>>;

/// <summary>
/// Handles tenant-scoped display model lookups.
/// </summary>
public sealed class GetDisplayModelsQueryHandler(IDisplayModelReadRepository displayModelReadRepository)
    : IQueryHandler<GetDisplayModelsQuery, ErrorOr<GetDisplayModelsResponse>>
{
    private readonly IDisplayModelReadRepository displayModelReadRepository = displayModelReadRepository;

    /// <summary>
    /// Retrieves display models for the requested tenant and maps them into the API response contract.
    /// </summary>
    /// <param name="request">Tenant-scoped query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Display model collection response or an error.</returns>
    public async ValueTask<ErrorOr<GetDisplayModelsResponse>> Handle(
        GetDisplayModelsQuery request,
        CancellationToken cancellationToken)
    {
        string tenantId = request.TenantId.Trim();

        IReadOnlyList<DisplayModelSnapshot> displayModels = await this.displayModelReadRepository
            .ListAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        List<GetDisplayModelItemResponse> mappedModels = new(displayModels.Count);
        foreach (DisplayModelSnapshot model in displayModels)
        {
            mappedModels.Add(new GetDisplayModelItemResponse
            {
                DisplayModelId = model.DisplayModelId,
                Name = model.Name,
                Width = model.Width,
                Height = model.Height,
            });
        }

        return new GetDisplayModelsResponse
        {
            DisplayModels = mappedModels,
        };
    }
}
