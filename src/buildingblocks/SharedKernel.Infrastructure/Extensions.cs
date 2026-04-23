using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Infrastructure.Middlewares;
using SharedKernel.Infrastructure.Options;

namespace SharedKernel.Infrastructure
{
    /// <summary>
    /// The extensions.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Allow all origins.
        /// </summary>
        public const string AllowAllOrigins = "AllowAll";

        /// <summary>
        /// Add the infrastructure.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="appOptions"></param>
        public static void AddBaseInfrastructure(
            this WebApplicationBuilder builder,
            AppOptions appOptions)
        {
            _ = appOptions;

            // 1. Core services
            builder.Services.Configure<JsonOptions>(options =>
            {
                options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            });

            builder.Services.AddHttpContextAccessor();

            builder.Services.AddRouting(options => options.LowercaseUrls = true);

            // 2. Authentication/Authorization baseline
            builder.Services.AddAuthentication();
            builder.Services.AddAuthorization();

            // 3. CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAllOrigins", policy =>
                    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
            });

            // 4. Forwarded headers
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                                         | ForwardedHeaders.XForwardedProto
                                         | ForwardedHeaders.XForwardedHost;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });

            // Add HTTP request logging so we can inspect forwarded headers at runtime.
            builder.Services.AddHttpLogging(options =>
            {
                options.LoggingFields = HttpLoggingFields.RequestPropertiesAndHeaders;
                options.RequestHeaders.Add("X-Forwarded-For");
                options.RequestHeaders.Add("X-Forwarded-Proto");
                options.RequestHeaders.Add("X-Forwarded-Host");
            });
        }

        /// <summary>
        /// Use the infrastructure.
        /// </summary>
        /// <param name="app">The app.</param>
        public static void UseBaseInfrastructure(
            this WebApplication app)
        {
            // Add global exception handler middleware here
            app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

            // Preserve Order
            app.UseCors(AllowAllOrigins);
            app.UseForwardedHeaders();

            // Enable HTTP logging after forwarded headers so logs reflect the proxied values.
            app.UseHttpLogging();

            app.Use(async (context, next) =>
            {
                app.Logger.LogInformation(
                    "Request Scheme={Scheme} Host={Host} XFP={XForwardedProto} XFH={XForwardedHost} XFF={XForwardedFor}",
                    context.Request.Scheme,
                    context.Request.Host.Value,
                    context.Request.Headers["X-Forwarded-Proto"].ToString(),
                    context.Request.Headers["X-Forwarded-Host"].ToString(),
                    context.Request.Headers["X-Forwarded-For"].ToString());

                if (context.Request.Headers.TryGetValue("X-Forwarded-Prefix", out var prefix))
                {
                    context.Request.PathBase = new PathString(prefix);
                }

                await next();
            });

            app.UseAuthentication();
            app.UseAuthorization();
        }
    }
}
