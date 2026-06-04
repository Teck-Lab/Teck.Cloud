// <copyright file="CreateLocationNodeEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062
using ErrorOr;
using FastEndpoints;
using Location.Application.Service.Features.CreateLocationNode.V1;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Location.Api.Endpoints.V1.Service;

/// <summary>
/// Handles create location node requests.
/// </summary>
public sealed class CreateLocationNodeEndpoint(ISender sender)
    : Endpoint<CreateLocationNodeRequest, CreateLocationNodeResponse>
{
    private readonly ISender sender = sender;

    /// <summary>
    /// Configures the endpoint route, version, and access rules.
    /// </summary>
    public override void Configure()
    {
        Post("/Service/LocationNodes");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    /// <summary>
    /// Handles the incoming request and writes the HTTP response.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    public override async Task HandleAsync(CreateLocationNodeRequest request, CancellationToken ct)
    {
        ErrorOr<string> tenantIdResult = ResolveTenantId(this.HttpContext);
        if (tenantIdResult.IsError)
        {
            ErrorOr<CreateLocationNodeResponse> responseError = tenantIdResult.Errors;
            await this.SendAsync(responseError, cancellation: ct).ConfigureAwait(false);
            return;
        }

        CreateLocationNodeCommand command = new(
            tenantIdResult.Value,
            request.Name,
            request.ParentLocationNodeId);

        ErrorOr<CreateLocationNodeResponse> commandResponse = await this.sender
            .Send(command, ct)
            .ConfigureAwait(false);

        await this.SendAsync(commandResponse, cancellation: ct).ConfigureAwait(false);
    }

    private static ErrorOr<string> ResolveTenantId(HttpContext httpContext)
    {
        if (!httpContext.Request.Headers.TryGetValue("X-TenantId", out var tenantValues))
        {
            return Error.Validation("Location.LocationNode.TenantIdRequired", "Header 'X-TenantId' is required.");
        }

        string tenantId = tenantValues.ToString().Trim();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Error.Validation("Location.LocationNode.TenantIdRequired", "Header 'X-TenantId' is required.");
        }

        return tenantId;
    }
}
