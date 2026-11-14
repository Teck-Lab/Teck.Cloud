using FastEndpoints;
using SharedKernel.Infrastructure.Hosting;
using SharedKernel.Infrastructure.MultiTenancy;

namespace SharedKernel.Api.TestHost;

/// <summary>A public response that changes whenever the endpoint executes.</summary>
public sealed class CachedProbeEndpoint : EndpointWithoutRequest
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Get("/cache-probe");
        AllowAnonymous();
        Options(builder => builder.WithMetadata(OperationScope.Global).CacheOutput(OutputCachingExtensions.PublicPolicy));
    }

    /// <inheritdoc/>
    public override Task HandleAsync(CancellationToken ct)
    {
        if (Query<bool>("private", isRequired: false))
        {
            HttpContext.Response.Headers.CacheControl = "private, no-store";
        }

        if (Query<bool>("cookie", isRequired: false))
        {
            HttpContext.Response.Cookies.Append("session", "fixture", new CookieOptions { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict });
        }

        return HttpContext.Response.WriteAsync(Guid.NewGuid().ToString("N"), ct);
    }
}
