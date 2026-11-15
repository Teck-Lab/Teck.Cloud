namespace SharedKernel.Infrastructure.Auth;

internal static class IdentityTransportPolicy
{
    internal static void Validate(Uri endpoint, IdentityTransport transport)
    {
        if (!Enum.IsDefined(transport))
        {
            throw new ArgumentOutOfRangeException(nameof(transport));
        }

        if (!endpoint.IsAbsoluteUri || endpoint.UserInfo.Length != 0 || endpoint.Fragment.Length != 0
            || (!string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
                && (!string.Equals(endpoint.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) || transport == IdentityTransport.Https)))
        {
            throw new InvalidOperationException("Identity credentials require HTTPS or an explicit protected HTTP transport.");
        }
    }
}
