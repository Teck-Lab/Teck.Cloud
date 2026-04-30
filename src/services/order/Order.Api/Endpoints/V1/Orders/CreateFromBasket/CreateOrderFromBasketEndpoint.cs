// <copyright file="CreateOrderFromBasketEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable CA1515

using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using Order.Application.Orders.Features.CreateOrderFromBasket.V1;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Order.Api.Endpoints.V1.Orders.CreateFromBasket;

/// <summary>
/// Creates an order from basket with strict synchronous catalog revalidation.
/// </summary>
public sealed class CreateOrderFromBasketEndpoint(ISender sender)
    : Endpoint<CreateOrderFromBasketRequest, CreateOrderFromBasketResponse>
{
    private readonly ISender sender = sender;

    /// <inheritdoc/>
    public override void Configure()
    {
        Post("/Orders/from-basket");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("order", "create");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
        Validator<CreateOrderFromBasketValidator>();
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CreateOrderFromBasketRequest request, CancellationToken ct)
    {
        CreateOrderFromBasketCommand command = new(request.TenantId, request.CustomerId, request.BasketId);
        ErrorOr<CreateOrderFromBasketResponse> result = await this.sender.Send(command, ct).ConfigureAwait(false);
        await this.SendAsync(result, cancellation: ct).ConfigureAwait(false);
    }
}
