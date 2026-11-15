using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Core.MultiTenancy;

namespace SharedKernel.Infrastructure.Auth;

/// <summary>Async credentials for generated gRPC clients, independent of HttpContext.</summary>
public static class GrpcCredentialsExtensions
{
    /// <summary>Propagates an audience-scoped token after scope validation; the receiver resolves tenants from signed claims.</summary>
    /// <param name="builder">The gRPC client builder.</param>
    /// <param name="audience">The required downstream audience.</param>
    /// <param name="transport">HTTPS unless explicitly protected by ambient mTLS or an isolated development boundary.</param>
    /// <returns>The configured client builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The transport mode is unknown.</exception>
    public static IHttpClientBuilder AddOperationCredentials(this IHttpClientBuilder builder, string audience, IdentityTransport transport = IdentityTransport.Https)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        if (!Enum.IsDefined(transport))
        {
            throw new ArgumentOutOfRangeException(nameof(transport));
        }

        _ = builder.ConfigureChannel(options => options.UnsafeUseInsecureChannelCallCredentials = transport != IdentityTransport.Https);
        return builder.AddCallCredentials(async (context, metadata, provider) =>
        {
            IdentityTransportPolicy.Validate(new Uri(context.ServiceUrl), transport);
            _ = provider.GetRequiredService<OperationContext>().Scope;
            var token = await provider.GetRequiredService<IAccessTokenProvider>()
                .GetTokenAsync(audience, context.CancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("The token provider returned no credentials.");
            }

            metadata.Add("authorization", "Bearer " + token);
        });
    }
}
