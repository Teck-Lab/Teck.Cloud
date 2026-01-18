using Catalog.Application.Brands.Features.DeleteBrands.V1;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using SharedKernel.Infrastructure.Endpoints;

namespace Catalog.Api.Endpoints.Brands.BulkDeleteBrands.V1
{
    /// <summary>
    /// The bulk delete brands endpoint.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="BulkDeleteBrandsEndpoint"/> class.
    /// </remarks>
    /// <param name="mediatr">The mediatr.</param>
    internal class BulkDeleteBrandsEndpoint(ISender mediatr) : Endpoint<DeleteBrandsRequest, NoContent>
    {
        /// <summary>
        /// The mediatr.
        /// </summary>
        private readonly ISender _mediatr = mediatr;

        /// <summary>
        /// Configure the endpoint.
        /// </summary>
        public override void Configure()
        {
            Post("/Brands/bulk/delete");
            Options(ep => ep.RequireProtectedResource("brands", "update"));
            Version(0);
            Summary(ep =>
            {
                ep.Summary = "Bulk delete brands";
            });
        }

        /// <summary>
        /// Handle the request.
        /// </summary>
        /// <param name="req"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public override async Task HandleAsync(DeleteBrandsRequest req, CancellationToken ct)
        {
            DeleteBrandsCommand command = new(req.Ids);
            ErrorOr<Deleted> commandResponse = await _mediatr.Send(command, ct);
            await this.SendNoContentResponseAsync(commandResponse, cancellation: ct);
        }
    }
}
