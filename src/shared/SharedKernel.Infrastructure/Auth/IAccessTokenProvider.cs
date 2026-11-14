namespace SharedKernel.Infrastructure.Auth;

/// <summary>Provides a token for the explicit downstream audience and current actor.</summary>
public interface IAccessTokenProvider
{
    /// <summary>Obtains a delegated or service token, including background operations.</summary>
    /// <param name="audience">The intended service audience.</param>
    /// <param name="cancellationToken">Call cancellation/deadline.</param>
    /// <returns>The access token, never a loggable value.</returns>
    Task<string> GetTokenAsync(string audience, CancellationToken cancellationToken);
}
