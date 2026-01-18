using Catalog.Application.Categories.Features.GetCategoryById.V1;
using Catalog.Application.Categories.Response;
using ErrorOr;
using FastEndpoints;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;

namespace Catalog.Api.Endpoints.Categories.GetCategoryById.V1;

/// <summary>
/// Endpoint for getting a category by ID.
/// </summary>
internal class GetCategoryByIdEndpoint : Endpoint<GetCategoryByIdRequest, CategoryResponse>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetCategoryByIdEndpoint"/> class.
    /// </summary>
    /// <param name="mediator">The mediator.</param>
    public GetCategoryByIdEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public override void Configure()
    {
        Get("api/v1/categories/{Id}");
        AllowAnonymous();
        Description(option => option
            .WithName("GetCategoryById")
            .Produces<CategoryResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Categories"));
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(GetCategoryByIdRequest req, CancellationToken ct)
    {
        var query = new GetCategoryByIdQuery(req.Id);
        ErrorOr<CategoryResponse> queryResponse = await _mediator.Send(query, ct);
        await this.SendAsync(queryResponse, cancellation: ct);
    }
}
