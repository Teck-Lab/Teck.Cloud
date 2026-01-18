using Catalog.Api.Endpoints.Brands.GetBrandById.V1;
using Catalog.Application.Brands.Features.CreateBrand.V1;
using Catalog.Application.Brands.Features.Responses;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;

namespace Catalog.Api.Endpoints.Brands.CreateBrand.V1
{
    /// <summary>
    /// The create brand endpoint.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CreateBrandEndpoint"/> class.
    /// </remarks>
    /// <param name="mediator">The mediatr.</param>
    internal class CreateBrandEndpoint(ISender mediator) : Endpoint<CreateBrandRequest, BrandResponse>
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
            Post("/Brands");
            Options(ep => ep.RequireProtectedResource("brand", "create")/*.AddEndpointFilter<IdempotentAPIEndpointFilter>()*/);
            Validator<CreateBrandValidator>();
            Version(1, 3);
        }

        /// <summary>
        /// Handle the request.
        /// </summary>
        /// <param name="req"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public override async Task HandleAsync(CreateBrandRequest req, CancellationToken ct)
        {
            CreateBrandCommand command = new(req.Name, req.Description, req.Website);
            ErrorOr<BrandResponse> commandResponse = await _mediator.Send(command, ct);
            await this.SendCreatedAtAsync<GetBrandByIdEndpoint, ErrorOr<BrandResponse>>(routeValues: new { commandResponse.Value?.Id }, commandResponse, cancellation: ct);
        }
    }
}
