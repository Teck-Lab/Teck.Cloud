using Catalog.Application.Brands.Features.DeleteBrand.V1;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using SharedKernel.Infrastructure.Endpoints;

namespace Catalog.Api.Endpoints.Brands.DeleteBrand.V1
{
    /// <summary>
    /// The delete brand endpoint.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="DeleteBrandEndpoint"/> class.
    /// </remarks>
    /// <param name="mediator">The mediatr.</param>
    internal class DeleteBrandEndpoint(ISender mediator) : Endpoint<DeleteBrandRequest, NoContent>
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
            Delete("/Brands/{Id}");
            Options(ep => ep.RequireProtectedResource("brand", "delete"));
            Version(1);
            Validator<DeleteBrandValidator>();
        }

        /// <summary>
        /// Handle the request.
        /// </summary>
        /// <param name="req"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public override async Task HandleAsync(DeleteBrandRequest req, CancellationToken ct)
        {
            DeleteBrandCommand command = new(req.Id);
            ErrorOr<Deleted> commandResponse = await _mediator.Send(command, ct);
            await this.SendNoContentResponseAsync(commandResponse, cancellation: ct);
        }
    }
}
