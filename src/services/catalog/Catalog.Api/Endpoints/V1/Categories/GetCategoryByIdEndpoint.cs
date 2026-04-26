// <copyright file="GetCategoryByIdEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Catalog.Application.Categories.Features.GetCategoryById.V1;
using Catalog.Application.Categories.Response;
using ErrorOr;
using FastEndpoints;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Catalog.Api.Endpoints.V1.Categories;

public sealed class GetCategoryByIdEndpoint(IMediator mediator) : Endpoint<GetCategoryByIdRequest, CategoryResponse>
{
    private readonly IMediator mediator = mediator;

    public override void Configure()
    {
        Get("/categories/{Id:guid}");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    public override async Task HandleAsync(GetCategoryByIdRequest request, CancellationToken ct)
    {
        GetCategoryByIdQuery query = new(request.Id);
        ErrorOr<CategoryResponse> queryResponse = await mediator.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(queryResponse, cancellation: ct).ConfigureAwait(false);
    }
}
