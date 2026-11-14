using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace SharedKernel.Infrastructure.Auth;

/// <summary>Registers the token client with bounded HTTP behavior and no credential-bearing redirects.</summary>
public static class OAuthRegistrationExtensions
{
    /// <summary>Adds OAuth transport; services choose delegation versus client credentials in their token provider.</summary>
    /// <param name="services">Host registrations.</param>
    /// <param name="options">Secret-supplied, trusted OAuth settings.</param>
    /// <returns>The configured HTTP client builder.</returns>
    public static IHttpClientBuilder AddOAuthTokenClient(this IServiceCollection services, OAuthClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _ = services.AddSingleton(options);
        var builder = services.AddHttpClient<OAuthTokenClient>(client => client.Timeout = TimeSpan.FromSeconds(20))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false });
        _ = builder.AddStandardResilienceHandler(resilience =>
        {
            resilience.Retry.DisableForUnsafeHttpMethods();
            resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
            resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
        });
        return builder;
    }
}
