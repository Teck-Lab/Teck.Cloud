using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Core.MultiTenancy;

namespace SharedKernel.Infrastructure.Auth;

/// <summary>Async credentials for generated gRPC clients, independent of HttpContext.</summary>
public static class GrpcCredentialsExtensions
{
    /// <summary>Propagates a token and established operation scope over TLS.</summary>
    /// <param name="builder">The gRPC client builder.</param>
    /// <param name="audience">The required downstream audience.</param>
    /// <returns>The configured client builder.</returns>
    public static IHttpClientBuilder AddOperationCredentials(this IHttpClientBuilder builder, string audience)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        return builder.AddCallCredentials(async (context, metadata, provider) =>
        {
            var operation = provider.GetRequiredService<OperationContext>();
            var scope = operation.Scope;
            var token = await provider.GetRequiredService<IAccessTokenProvider>()
                .GetTokenAsync(audience, context.CancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("The token provider returned no credentials.");
            }

            metadata.Add("authorization", "Bearer " + token);
            if (scope.TenantId is { } tenant)
            {
                metadata.Add("x-tenant-id", tenant);
            }
        });
    }
}
