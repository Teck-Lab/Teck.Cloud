using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serilog;

namespace SharedKernel.Infrastructure.Middlewares
{
    /// <summary>
    /// Middleware that globally catches unhandled exceptions, logs them using Serilog,
    /// and returns a <see cref="ProblemDetails"/> response including
    /// traceId, correlationId, and a list of error details.
    /// </summary>
    public class GlobalExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IDiagnosticContext _diagnosticContext;
        private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalExceptionHandlerMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware delegate in the pipeline.</param>
        /// <param name="diagnosticContext">Serilog diagnostic context to enrich logs.</param>
        /// <param name="logger">The logger to use for error logging.</param>
        public GlobalExceptionHandlerMiddleware(
            RequestDelegate next,
            IDiagnosticContext diagnosticContext,
            ILogger<GlobalExceptionHandlerMiddleware> logger)
        {
            _next = next;
            _diagnosticContext = diagnosticContext;
            _logger = logger;
        }

        /// <summary>
        /// Processes an HTTP request and catches unhandled exceptions.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>A task that represents the completion of request processing.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception exception)
            {
                // Obtain trace and correlation IDs for diagnostics and response
                var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
                var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? traceId;
                var user = context.User?.Identity?.Name ?? "anonymous";
                var path = context.Request.Path;

                // Enrich Serilog diagnostic context with identifiers and request info
                _diagnosticContext.Set("TraceId", traceId);
                _diagnosticContext.Set("CorrelationId", correlationId);
                _diagnosticContext.Set("Path", path);
                _diagnosticContext.Set("User", user);

                // Log the exception with Serilog including contextual information
                _logger.LogError(
                    exception,
                    "Unhandled exception at {Path} [TraceId: {TraceId}, CorrelationId: {CorrelationId}, User: {User}]",
                    path,
                    traceId,
                    correlationId,
                    user
                );

                // Build ProblemDetails response payload for clients
                var problem = new ProblemDetails
                {
                    Status = 500,
                    Title = "Internal Server Error",
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                    Detail = "An unexpected error occurred. Please try again later.",
                    Extensions =
                    {
                        ["traceId"] = traceId,
                        ["correlationId"] = correlationId,
                        ["details"] = new[]
                        {
                            new { name = "server", reason = "An unexpected error occurred. Please contact support if the problem persists." }
                        }
                    }
                };

                // Set response status and content type, then write JSON response
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
            }
        }
    }
}
