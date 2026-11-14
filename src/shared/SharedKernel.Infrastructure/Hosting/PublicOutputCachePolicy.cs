using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using SharedKernel.Infrastructure.MultiTenancy;

namespace SharedKernel.Infrastructure.Hosting;

/// <summary>Restricts the shared public response policy to explicitly global, anonymous requests.</summary>
internal sealed class PublicOutputCachePolicy : IOutputCachePolicy
{
    internal static readonly PublicOutputCachePolicy Instance = new();

    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        var http = context.HttpContext;
        if (http.GetEndpoint()?.Metadata.GetMetadata<OperationScope>() != OperationScope.Global
            || http.User.Identity?.IsAuthenticated == true
            || http.Request.Headers.Authorization.Count > 0
            || http.Request.Headers.Cookie.Count > 0)
        {
            Disable(context);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation) => ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        var control = context.HttpContext.Response.GetTypedHeaders().CacheControl;
        if (control?.Private == true || control?.NoStore == true || control?.NoCache == true)
        {
            Disable(context);
        }

        return ValueTask.CompletedTask;
    }

    private static void Disable(OutputCacheContext context)
    {
        context.AllowCacheLookup = false;
        context.AllowCacheStorage = false;
    }
}
