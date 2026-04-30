// <copyright file="GetBasketByIdEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable CA1515

using Basket.Application.Basket.Features.AddItemToBasket.V1;
using Basket.Application.Basket.Features.GetBasketById.V1;
using ErrorOr;
using FastEndpoints;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;

namespace Basket.Api.Endpoints.V1.Basket.GetById;

/// <summary>
/// Gets a basket snapshot by identifier.
/// </summary>
public sealed class GetBasketByIdEndpoint(ISender sender)
    : Endpoint<GetBasketByIdRequest, AddItemToBasketResponse>
{
    private readonly ISender sender = sender;

    /// <inheritdoc/>
    public override void Configure()
    {
        Get("/Basket/{BasketId:guid}");
        Version(1);
        Validator<GetBasketByIdValidator>();
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(GetBasketByIdRequest request, CancellationToken ct)
    {
        bool isSignedIn = HttpContext.User.Identity?.IsAuthenticated == true;
        GetBasketByIdQuery query = new(request.BasketId, request.TenantId, request.CustomerId, isSignedIn);
        ErrorOr<AddItemToBasketResponse> result = await this.sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(result, cancellation: ct).ConfigureAwait(false);
    }
}
