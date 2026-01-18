using ErrorOr;
using FastEndpoints;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SharedKernel.Infrastructure.Endpoints
{
    /// <summary>
    /// Provides extension methods for <see cref="IEndpoint"/> to send responses
    /// while handling errors consistently using the ErrorOr pattern integrated with FastEndpoints.
    /// </summary>
    public static class FastEndpointsExtensions
    {
        /// <summary>
        /// Sends a 201 Created response asynchronously.
        /// </summary>
        /// <typeparam name="TEndpoint">The endpoint type.</typeparam>
        /// <typeparam name="TResponse">The response type implementing <see cref="IErrorOr"/>.</typeparam>
        /// <param name="ep">The endpoint instance.</param>
        /// <param name="routeValues">Route values for URL generation.</param>
        /// <param name="response">The response result.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SendCreatedAtAsync<TEndpoint, TResponse>(
            this IEndpoint ep,
            object? routeValues,
            TResponse response,
            CancellationToken cancellation = default)
            where TEndpoint : IEndpoint
            where TResponse : IErrorOr<object>
        {
            return !response.IsError
                ? ep.HttpContext.Response.SendCreatedAtAsync<TEndpoint>(routeValues, response.Value, cancellation: cancellation)
                : HandleErrorOr(ep, response, cancellation);
        }

        /// <summary>
        /// Sends an HTTP response asynchronously containing the value of the <paramref name="response"/> if successful,
        /// or sends an error response if the <paramref name="response"/> contains errors.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response object, which implements <see cref="IErrorOr{TValue}"/>.</typeparam>
        /// <param name="ep">The endpoint instance invoking this method.</param>
        /// <param name="response">An instance of <see cref="IErrorOr"/> representing the result.</param>
        /// <param name="cancellation">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that completes when the response has been sent.</returns>
        public static Task SendAsync<TResponse>(
            this IEndpoint ep,
            TResponse response,
            CancellationToken cancellation = default)
            where TResponse : IErrorOr<object>
        {
            return !response.IsError
                ? ep.HttpContext.Response.SendAsync(response.Value, cancellation: cancellation)
                : HandleErrorOr(ep, response, cancellation);
        }

        /// <summary>
        /// Send no content response asynchronously.
        /// </summary>
        /// <typeparam name="TResponse"/>
        /// <param name="ep">The ep.</param>
        /// <param name="response">The response.</param>
        /// <param name="cancellation">The cancellation.</param>
        /// <returns>A Task.</returns>
        public static Task SendNoContentResponseAsync<TResponse>(this IEndpoint ep, TResponse response, CancellationToken cancellation = default)
            where TResponse : IErrorOr
        {
            return !response.IsError
                ? ep.HttpContext.Response.SendNoContentAsync(cancellation: cancellation)
                : HandleErrorOr(ep, response, cancellation);
        }

        /// <summary>
        /// Handles sending error responses based on the <see cref="IErrorOr"/> pattern.
        /// This method converts errors to appropriate HTTP status codes and formats the response
        /// to include correlation and trace identifiers for easier diagnostics.
        /// </summary>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="ep">The endpoint instance invoking this method.</param>
        /// <param name="response">An instance of <see cref="IErrorOr"/> representing the result.</param>
        /// <param name="cancellation">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that completes when the error response has been sent.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the error response does not contain any recognizable errors.</exception>
        private static Task HandleErrorOr<TResponse>(
    IEndpoint ep,
    TResponse response,
    CancellationToken cancellation = default)
    where TResponse : IErrorOr
        {
            var http = ep.HttpContext;
            var traceId = http.TraceIdentifier;

            var correlationId = http.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                               ?? Guid.NewGuid().ToString();

            http.Response.Headers["X-Correlation-ID"] = correlationId;

            if (response.Errors?.TrueForAll(error => error.Type == ErrorType.Validation) == true)
            {
                var failures = response.Errors
                    .Select(error => new ValidationFailure(error.Code, error.Description))
                    .ToList();

                var problemDetails = new ValidationProblemDetails(
                    failures.GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
                        .ToDictionary(
                            keySelector: group => group.Key,
                            elementSelector: group => group.Select(failure => failure.ErrorMessage).ToArray()))
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "One or more validation errors occurred.",
                    Status = StatusCodes.Status400BadRequest,
                    Instance = http.Request.Path
                };

                problemDetails.Extensions["traceId"] = traceId;
                problemDetails.Extensions["correlationId"] = correlationId;

                http.Response.StatusCode = StatusCodes.Status400BadRequest;
                http.Response.ContentType = "application/problem+json";
                return http.Response.WriteAsJsonAsync(problemDetails, cancellation);
            }

            var error = response.Errors?.FirstOrDefault(error => error.Type != ErrorType.Validation);
            if (error == null)
                throw new InvalidOperationException("No matching endpoint error.");

            var statusCode = error!.Value.Type switch
            {
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status400BadRequest
            };

            var genericProblem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = Enum.GetName<ErrorType>(error.Value.Type), // Fix for EPC20
                Status = statusCode,
                Instance = http.Request.Path
            };

            genericProblem.Extensions["traceId"] = traceId;
            genericProblem.Extensions["correlationId"] = correlationId;
            genericProblem.Extensions["errors"] = new[]
            {
                new { name = error.Value.Code, reason = error.Value.Description }
            };

            http.Response.StatusCode = statusCode;
            http.Response.ContentType = "application/problem+json";
            return http.Response.WriteAsJsonAsync(genericProblem, cancellationToken: cancellation);
        }
    }
}
