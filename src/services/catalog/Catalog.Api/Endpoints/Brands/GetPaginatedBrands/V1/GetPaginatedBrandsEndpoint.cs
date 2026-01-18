using Catalog.Application.Brands.Features.GetPaginatedBrands.V1;
using Catalog.Application.Brands.Features.Responses;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Core.Pagination;
using SharedKernel.Infrastructure.Endpoints;

namespace Catalog.Api.Endpoints.Brands.GetPaginatedBrands.V1
{
    /// <summary>
    /// The get paginated brands endpoint.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="GetPaginatedBrandsEndpoint"/> class.
    /// </remarks>
    /// <param name="mediator">The mediatr.</param>
    internal class GetPaginatedBrandsEndpoint(ISender mediator) : Endpoint<GetPaginatedBrandsRequest, PagedList<BrandResponse>>
    {
        /// <summary>
        /// The mediatr.
        /// </summary>
        private readonly ISender _mediator = mediator;

        /// <summary>
        /// Configure the endpoint.
        /// </summary>
        public override void Configure()
        {
            Get("/Brands");
            Options(ep => ep.RequireProtectedResource("brand", "list"));
            Version(1);
            Validator<GetPaginatedBrandsValidator>();
        }

        /// <summary>
        /// Handle the request.
        /// </summary>
        /// <param name="req"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public override async Task HandleAsync(GetPaginatedBrandsRequest req, CancellationToken ct)
        {
            GetPaginatedBrandsQuery query = new(req.Page, req.Size, req.Keyword);
            ErrorOr<PagedList<BrandResponse>> queryResponse = await _mediator.Send(query, ct);
            await this.SendAsync(queryResponse, cancellation: ct);
        }
    }
}
