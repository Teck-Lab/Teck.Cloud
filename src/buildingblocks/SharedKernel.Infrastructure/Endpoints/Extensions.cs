using System.Reflection;
using System.Text.Json.Serialization;
using CorrelationId.Abstractions;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Infrastructure.Endpoints
{
    /// <summary>
    /// The extensions.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Add fast endpoints extension.
        /// </summary>
        /// <param name="services">The services.</param>
        /// <param name="validatorAssembly"></param>
        /// <param name="apiAssembly"></param>
        public static IServiceCollection AddFastEndpointsInfrastructure(
            this IServiceCollection services,
            Assembly? validatorAssembly = null,
            Assembly? apiAssembly = null)
        {
            services.AddFastEndpoints(ep =>
            {
                var assemblies = new List<Assembly>();
                if (apiAssembly is not null)
                {
                    assemblies.Add(apiAssembly);
                }
                if (validatorAssembly is not null)
                {
                    assemblies.Add(validatorAssembly);
                    services.AddValidatorsFromAssembly(validatorAssembly, filter: filter => filter.ValidatorType.BaseType?.GetGenericTypeDefinition() != typeof(FastEndpoints.Validator<>));
                }
                ep.Assemblies = assemblies.ToArray();
            }).AddIdempotency();

            return services;
        }

        /// <summary>
        /// Use swagger extension.
        /// </summary>
        /// <param name="app">The app.</param>
        public static IApplicationBuilder UseFastEndpointsInfrastructure(this IApplicationBuilder app)
        {
            app.UseOutputCache().UseFastEndpoints(config =>
            {
                config.Errors.ResponseBuilder = (failures, context, statusCode) =>
                {
                    var accessor = context.RequestServices.GetService<ICorrelationContextAccessor>();
                    var correlationId = accessor?.CorrelationContext?.CorrelationId ?? Guid.NewGuid().ToString();

                    context.Response.Headers["X-Correlation-ID"] = correlationId;

                    var problemDetails = new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(
                        failures.GroupBy(failure => failure.PropertyName)
                                .ToDictionary(
                                    keySelector: group => group.Key,
                                    elementSelector: group => group.Select(failure => failure.ErrorMessage).ToArray()))
                    {
                        Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                        Title = "One or more validation errors occurred.",
                        Status = statusCode,
                        Instance = context.Request.Path
                    };

                    problemDetails.Extensions["traceId"] = context.TraceIdentifier;
                    problemDetails.Extensions["correlationId"] = correlationId;

                    return problemDetails;
                };

                config.Serializer.Options.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                config.Endpoints.RoutePrefix = "api";
                config.Versioning.Prefix = "v";
                config.Versioning.PrependToRoute = true;
                config.Versioning.DefaultVersion = 1;
            });

            return app;
        }
    }
}
