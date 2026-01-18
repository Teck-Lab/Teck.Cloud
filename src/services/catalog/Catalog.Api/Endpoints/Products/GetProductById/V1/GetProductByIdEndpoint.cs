using Catalog.Application.Features.Products.GetProductById.V1;
using Catalog.Application.Products.Features.GetProductById.V1;
using Catalog.Application.Products.Responses;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;

namespace Catalog.Api.Endpoints.Products.GetProductById.V1
{
    /// <summary>
    /// The get product by id endpoint.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="GetProductByIdEndpoint"/> class.
    /// </remarks>
    /// <param name="mediator">The mediatr.</param>
    internal class GetProductByIdEndpoint(ISender mediator) : Endpoint<GetProductByIdRequest, ProductResponse>
    {
        private readonly ISender _mediator = mediator;

        /// <summary>
        /// Configures the endpoint settings such as route, authorization, version, and validator.
        /// </summary>
        public override void Configure()
        {
            Get("/Products/{Id}");
            AllowAnonymous();
            Options(ep => ep.RequireProtectedResource("products", "read"));
            Version(0);
            Validator<GetProductByIdValidator>();
        }

        /// <summary>
        /// Handles the HTTP GET request to retrieve a product by its ID.
        /// </summary>
        /// <param name="req">The request containing the product ID.</param>
        /// <param name="ct">The cancellation token.</param>
        public override async Task HandleAsync(GetProductByIdRequest req, CancellationToken ct)
        {
            GetProductByIdQuery query = new(req.ProductId);
            ErrorOr<ProductResponse> queryResponse = await _mediator.Send(query, ct);
            await this.SendAsync(queryResponse, ct);
        }
    }
}
