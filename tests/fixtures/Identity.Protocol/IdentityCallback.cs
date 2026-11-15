using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Identity.Protocol;

internal sealed class IdentityCallback(WebApplication application) : IAsyncDisposable
{
    internal string RedirectUri { get; } = new UriBuilder(application.Urls.Single()) { Host = "localhost", Path = "/mfa-callback" }.Uri.AbsoluteUri;

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => application.DisposeAsync();

    internal static async Task<IdentityCallback> StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();

        // Authorization codes in callback query strings must not enter test logs.
        _ = builder.Logging.ClearProviders();
        _ = builder.WebHost.UseUrls("http://127.0.0.1:0");
        var application = builder.Build();
        _ = application.MapGet("/mfa-callback", () => Results.Text("Identity callback received"));
        await application.StartAsync(cancellationToken).ConfigureAwait(false);
        return new IdentityCallback(application);
    }
}
