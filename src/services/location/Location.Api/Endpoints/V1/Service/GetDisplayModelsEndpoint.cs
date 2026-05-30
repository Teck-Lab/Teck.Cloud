// <copyright file="GetDisplayModelsEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using ErrorOr;
using FastEndpoints;
using Location.Application.Service.Features.GetDisplayModels.V1;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Location.Api.Endpoints.V1.Service;

public sealed class GetDisplayModelsEndpoint(ISender sender)
    : EndpointWithoutRequest<GetDisplayModelsResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Get("/Service/Displays");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        ErrorOr<string> tenantIdResult = ResolveTenantId(this.HttpContext);
        if (tenantIdResult.IsError)
        {
            ErrorOr<GetDisplayModelsResponse> responseError = tenantIdResult.Errors;
            await this.SendAsync(responseError, cancellation: ct).ConfigureAwait(false);
            return;
        }

        ErrorOr<GetDisplayModelsResponse> queryResponse = await this.sender
            .Send(new GetDisplayModelsQuery(tenantIdResult.Value), ct)
            .ConfigureAwait(false);
        await this.SendAsync(queryResponse, cancellation: ct).ConfigureAwait(false);
    }

    private static ErrorOr<string> ResolveTenantId(HttpContext httpContext)
    {
        if (!httpContext.Request.Headers.TryGetValue("X-TenantId", out var tenantValues))
        {
            return Error.Validation("Location.DisplayModels.TenantIdRequired", "Header 'X-TenantId' is required.");
        }

        string tenantId = tenantValues.ToString().Trim();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Error.Validation("Location.DisplayModels.TenantIdRequired", "Header 'X-TenantId' is required.");
        }

        return tenantId;
    }
}
