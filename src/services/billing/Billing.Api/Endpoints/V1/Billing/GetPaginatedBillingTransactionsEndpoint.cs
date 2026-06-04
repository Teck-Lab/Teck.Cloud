// <copyright file="GetPaginatedBillingTransactionsEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062
using Billing.Application.Billing.Features.GetPaginatedBillingTransactions.V1;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Core.Pagination;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Billing.Api.Endpoints.V1.Billing;

/// <summary>
/// Endpoint for retrieving a paginated list of billing transactions.
/// </summary>
public sealed class GetPaginatedBillingTransactionsEndpoint(ISender sender)
    : Endpoint<GetPaginatedBillingTransactionsRequest, PagedList<GetPaginatedBillingTransactionsResponse>>
{
    private readonly ISender sender = sender;

    /// <inheritdoc/>
    public override void Configure()
    {
        Get("/Billing/Transactions");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("billing-transaction", "list");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
        Validator<GetPaginatedBillingTransactionsValidator>();
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(GetPaginatedBillingTransactionsRequest request, CancellationToken ct)
    {
        GetPaginatedBillingTransactionsQuery query = new(request.Page, request.Size, request.TenantId, request.Status);
        ErrorOr<PagedList<GetPaginatedBillingTransactionsResponse>> queryResponse =
            await this.sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(queryResponse, cancellation: ct).ConfigureAwait(false);
    }
}
