using Microsoft.Extensions.Configuration;
using SharedKernel.Infrastructure.Auth;
using Teck.Cloud.Commerce.Contracts;

namespace Teck.Cloud.Commerce.Gateway.Composition;

/// <summary>Trusted internal commerce endpoints and audiences used by a gateway.</summary>
public sealed class CommerceGatewayOptions
{
    /// <summary>Gets the Orders gRPC base address.</summary>
    public required Uri OrdersAddress { get; init; }

    /// <summary>Gets the Orders service audience used for credential acquisition.</summary>
    public required string OrdersAudience { get; init; }

    /// <summary>Gets the Licensing gRPC base address; required only by the customer edge.</summary>
    public Uri? LicensingAddress { get; init; }

    /// <summary>Gets the Licensing service audience used for credential acquisition.</summary>
    public required string LicensingAudience { get; init; }

    /// <summary>Gets the transport trust boundary for internal gRPC.</summary>
    public IdentityTransport Transport { get; init; }

    /// <summary>Gets the bounded per-call deadline.</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Loads and validates the documented <c lang="text">Commerce</c> settings.</summary>
    /// <param name="configuration">Host configuration.</param>
    /// <param name="requireLicensing">Whether the calling edge also needs Licensing.</param>
    /// <returns>The validated settings.</returns>
    /// <exception cref="InvalidOperationException">A required address or audience is missing or unsafe.</exception>
    public static CommerceGatewayOptions Load(IConfiguration configuration, bool requireLicensing)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var transport = ParseTransport(configuration["Commerce:Transport"]);
        var orders = ParseAddress(configuration["Commerce:Orders:Address"], "Commerce:Orders:Address", transport);
        var licensing = requireLicensing
            ? ParseAddress(configuration["Commerce:Licensing:Address"], "Commerce:Licensing:Address", transport)
            : null;
        var timeout = configuration.GetValue("Commerce:RequestTimeoutSeconds", 15);
        return new CommerceGatewayOptions
        {
            OrdersAddress = orders,
            OrdersAudience = CommerceAuth.OrdersAudience,
            LicensingAddress = licensing,
            LicensingAudience = CommerceAuth.LicensingAudience,
            Transport = transport,
            RequestTimeout = timeout is > 0 and <= 120 ? TimeSpan.FromSeconds(timeout) : throw new InvalidOperationException("Commerce:RequestTimeoutSeconds must be between 1 and 120."),
        };
    }

    private static Uri ParseAddress(string? value, string key, IdentityTransport transport) =>
        Uri.TryCreate(value, UriKind.Absolute, out var address)
            && address.UserInfo.Length == 0
            && address.Fragment.Length == 0
            && (string.Equals(address.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) || transport != IdentityTransport.Https)
            ? address
            : throw new InvalidOperationException(key + " must be an absolute HTTPS address, or HTTP only under an explicit protected transport.");

    private static IdentityTransport ParseTransport(string? value) =>
        string.IsNullOrWhiteSpace(value) ? IdentityTransport.Https : RequiredTransport(value);

    private static IdentityTransport RequiredTransport(string value) =>
        Enum.TryParse<IdentityTransport>(value, ignoreCase: true, out var transport) && Enum.IsDefined(transport)
            ? transport
            : throw new InvalidOperationException("Commerce:Transport must be Https, AmbientMesh or IsolatedDevelopment.");
}
