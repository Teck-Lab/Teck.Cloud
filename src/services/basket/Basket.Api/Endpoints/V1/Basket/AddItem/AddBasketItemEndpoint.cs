// <copyright file="AddBasketItemEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable CA1515

using Basket.Application.Basket.Features.AddItemToBasket.V1;
using ErrorOr;
using FastEndpoints;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;

namespace Basket.Api.Endpoints.V1.Basket.AddItem;

/// <summary>
/// Adds an item into a tenant/customer basket draft.
/// </summary>
public sealed class AddBasketItemEndpoint(ISender sender)
    : Endpoint<AddBasketItemRequest, AddItemToBasketResponse>
{
    private readonly ISender sender = sender;

    /// <inheritdoc/>
    public override void Configure()
    {
        Post("/Basket/items");
        Version(1);
        Validator<AddBasketItemValidator>();
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(AddBasketItemRequest request, CancellationToken ct)
    {
        bool isSignedIn = HttpContext.User.Identity?.IsAuthenticated == true;

        AddItemToBasketCommand command = new(
            request.TenantId,
            request.CustomerId,
            isSignedIn,
            request.ProductId,
            request.Quantity);

        ErrorOr<AddItemToBasketResponse> result = await this.sender.Send(command, ct).ConfigureAwait(false);
        await this.SendAsync(result, cancellation: ct).ConfigureAwait(false);
    }
}
