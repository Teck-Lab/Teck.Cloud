using Catalog.Application.Brands.Features.GetBrandById.V1;
using Catalog.Application.Brands.Features.Responses;
using ErrorOr;
using FastEndpoints;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;

namespace Catalog.Api.Endpoints.Brands.GetBrandById.V1;

/// <summary>
/// Endpoint for getting a brand by its ID.
/// </summary>
internal sealed class GetBrandByIdEndpoint : Endpoint<GetBrandByIdRequest, BrandResponse>
{
    private readonly ISender _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetBrandByIdEndpoint"/> class.
    /// </summary>
    /// <param name="mediator">The mediator used to send queries.</param>
    public GetBrandByIdEndpoint(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Configures the endpoint route and version.
    /// </summary>
    public override void Configure()
    {
        Get("/Brands/{Id}");
        Validator<GetBrandByIdValidator>();
        Version(1);
    }

    /// <summary>
    /// Handles the HTTP GET request to retrieve a brand by its ID.
    /// </summary>
    /// <param name="req">The request containing the brand ID.</param>
    /// <param name="ct">The cancellation token.</param>
    public override async Task HandleAsync(GetBrandByIdRequest req, CancellationToken ct)
    {
        var query = new GetBrandByIdQuery(req.Id);
        ErrorOr<BrandResponse> queryResponse = await _mediator.Send(query, ct);
        await this.SendAsync(queryResponse, cancellation: ct);
    }
}
