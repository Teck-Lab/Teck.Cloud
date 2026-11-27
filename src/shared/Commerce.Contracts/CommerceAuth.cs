using System.Text.Json;
using Keycloak.AuthServices.Authorization;
using Keycloak.AuthServices.Common.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Infrastructure.Auth;

namespace Teck.Cloud.Commerce.Contracts;

/// <summary>Shared commerce audiences, permissions and host authentication composition.</summary>
/// <remarks>
/// Hosts bind <see cref="CommerceAuthOptions"/> from <see cref="CommerceAuthOptions.ConfigurationSection"/>.
/// Each host also registers its own <c lang="csharp">ITenantDirectory</c> (Keycloak-backed store) before serving.
/// </remarks>
public static class CommerceAuth
{
    /// <summary>The customer gateway's existing Keycloak audience.</summary>
    public const string CustomerGatewayAudience = "customer-gateway";

    /// <summary>The admin gateway's existing Keycloak audience.</summary>
    public const string AdminGatewayAudience = "admin-gateway";

    /// <summary>The Orders service's internal audience.</summary>
    public const string OrdersAudience = "commerce-orders";

    /// <summary>The Licensing service's internal audience.</summary>
    public const string LicensingAudience = "commerce-licensing";

    /// <summary>The customer permission and its same-named realm role.</summary>
    public const string CustomerPermission = "commerce.customer";

    /// <summary>The staff order-read permission and its same-named realm role.</summary>
    public const string OrdersReadPermission = "CommerceOrdersRead";

    /// <summary>The staff decision permission and its same-named realm role.</summary>
    public const string OrdersDecidePermission = "CommerceOrdersDecide";

    /// <summary>The staff placement-evidence permission and its same-named realm role.</summary>
    public const string PlacementRecordPermission = "CommercePlacementRecord";

    /// <summary>The customer gateway workload permission for internal license reads.</summary>
    public const string LicenseReadPermission = "CommerceLicenseRead";

    /// <summary>The Licensing workload permission for the internal entitlement check.</summary>
    public const string EntitlementCheckPermission = "CommerceEntitlementCheck";

    /// <summary>The customer policy for catalog, basket and order operations.</summary>
    public const string CustomerPolicy = CustomerPermission;

    /// <summary>The admin order-list and detail policy.</summary>
    public const string OrdersReadPolicy = OrdersReadPermission;

    /// <summary>The admin approve/reject decision policy.</summary>
    public const string OrdersDecidePolicy = OrdersDecidePermission;

    /// <summary>The admin placement-evidence policy.</summary>
    public const string PlacementRecordPolicy = PlacementRecordPermission;

    /// <summary>The internal license-read workload policy.</summary>
    public const string LicenseReadPolicy = LicenseReadPermission;

    /// <summary>The internal entitlement-check workload policy.</summary>
    public const string EntitlementCheckPolicy = EntitlementCheckPermission;

    /// <summary>The gRPC metadata key carrying the already-authorized tenant.</summary>
    public const string TenantMetadataKey = "teck-tenant-id";

    /// <summary>Reads <see cref="CommerceAuthOptions"/> from <see cref="CommerceAuthOptions.ConfigurationSection"/> settings.</summary>
    /// <param name="configuration">The host configuration.</param>
    /// <returns>Explicit, validated commerce identity settings.</returns>
    /// <exception cref="InvalidOperationException">A required or malformed setting is present.</exception>
    public static CommerceAuthOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(CommerceAuthOptions.ConfigurationSection);
        var realm = section[nameof(CommerceAuthOptions.Realm)];
        return new CommerceAuthOptions
        {
            ServerUrl = OptionalUri(section, nameof(CommerceAuthOptions.ServerUrl))
                ?? throw new InvalidOperationException("Commerce:Identity:ServerUrl is required."),
            Realm = string.IsNullOrWhiteSpace(realm)
                ? throw new InvalidOperationException("Commerce:Identity:Realm is required.")
                : realm,
            PublicIssuer = OptionalUri(section, nameof(CommerceAuthOptions.PublicIssuer)),
            Transport = OptionalTransport(section),
            TokenEndpoint = OptionalUri(section, nameof(CommerceAuthOptions.TokenEndpoint)),
            ClientId = NullIfBlank(section[nameof(CommerceAuthOptions.ClientId)]),
            ClientSecret = NullIfBlank(section[nameof(CommerceAuthOptions.ClientSecret)]),
        };
    }

    /// <summary>Adds customer gateway bearer authentication, the customer policy and outbound credentials.</summary>
    /// <param name="services">Host registrations.</param>
    /// <param name="options">Trusted identity and this gateway's OAuth client.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCommerceCustomerGatewayAuth(this IServiceCollection services, CommerceAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        services.TryAddScoped<OperationContext>();
        _ = services.AddFoundationKeycloakAuthentication(Keycloak(options, CustomerGatewayAudience));
        _ = services.AddAuthorizationBuilder().AddPolicy(CustomerPolicy, policy => policy
            .RequireAuthenticatedUser()
            .RequireOrganizationMembership()
            .RequireRealmRoles(CustomerPermission));
        AddOutboundCredentials(services, options);
        _ = services.AddScoped<IAccessTokenProvider, CustomerGatewayTokenProvider>();
        return services;
    }

    /// <summary>Adds admin gateway bearer authentication, the three staff policies and outbound credentials.</summary>
    /// <param name="services">Host registrations.</param>
    /// <param name="options">Trusted identity and this gateway's OAuth client.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCommerceAdminGatewayAuth(this IServiceCollection services, CommerceAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        services.TryAddScoped<OperationContext>();
        _ = services.AddFoundationKeycloakAuthentication(Keycloak(options, AdminGatewayAudience));
        AddStaffPolicies(services.AddAuthorizationBuilder());
        AddOutboundCredentials(services, options);
        _ = services.AddScoped<IAccessTokenProvider, AdminGatewayTokenProvider>();
        return services;
    }

    /// <summary>Adds Orders service authentication, independent staff/customer/workload policies and gRPC scope resolution.</summary>
    /// <param name="services">Host registrations.</param>
    /// <param name="options">Trusted identity settings.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCommerceOrdersServiceAuth(this IServiceCollection services, CommerceAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        services.TryAddScoped<OperationContext>();
        _ = services.AddFoundationKeycloakAuthentication(Keycloak(options, OrdersAudience));
        var authorization = services.AddAuthorizationBuilder();
        _ = authorization.AddPolicy(CustomerPolicy, policy => policy
            .RequireAuthenticatedUser()
            .RequireOrganizationMembership()
            .RequireRealmRoles(CustomerPermission));
        AddStaffPolicies(authorization);
        _ = authorization.AddPolicy(EntitlementCheckPolicy, policy => policy
            .RequireAuthenticatedUser()
            .RequireRealmRoles(EntitlementCheckPermission));
        _ = services.AddSingleton<CommerceScopeInterceptor>();
        _ = services.AddGrpc(grpc => grpc.Interceptors.Add<CommerceScopeInterceptor>());
        return services;
    }

    /// <summary>Adds Licensing service authentication, the internal license-read policy, gRPC scope resolution and Orders credentials.</summary>
    /// <param name="services">Host registrations.</param>
    /// <param name="options">Trusted identity and this service's OAuth client.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCommerceLicensingServiceAuth(this IServiceCollection services, CommerceAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        services.TryAddScoped<OperationContext>();
        _ = services.AddFoundationKeycloakAuthentication(Keycloak(options, LicensingAudience));
        _ = services.AddAuthorizationBuilder().AddPolicy(LicenseReadPolicy, policy => policy
            .RequireAuthenticatedUser()
            .RequireRealmRoles(LicenseReadPermission));
        _ = services.AddSingleton<CommerceScopeInterceptor>();
        _ = services.AddGrpc(grpc => grpc.Interceptors.Add<CommerceScopeInterceptor>());
        AddOutboundCredentials(services, options);
        _ = services.AddScoped<IAccessTokenProvider, LicensingTokenProvider>();
        return services;
    }

    /// <summary>Adds audience-scoped bearer credentials and the validated tenant metadata to a gRPC client.</summary>
    /// <param name="builder">The gRPC client builder.</param>
    /// <param name="audience">The required downstream audience.</param>
    /// <param name="transport">HTTPS unless explicitly protected by ambient mTLS or an isolated development boundary.</param>
    /// <returns>The configured client builder.</returns>
    public static IHttpClientBuilder AddCommerceOperationCredentials(this IHttpClientBuilder builder, string audience, IdentityTransport transport = IdentityTransport.Https)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.AddOperationCredentials(audience, transport);
        return builder.AddCallCredentials((_, metadata, provider) =>
        {
            var tenant = provider.GetRequiredService<OperationContext>().Scope.TenantId
                ?? throw new InvalidOperationException("Commerce calls require an established tenant scope.");
            metadata.Add(TenantMetadataKey, tenant);
            return Task.CompletedTask;
        });
    }

    /// <summary>Reads the validated bearer token from the current request.</summary>
    /// <param name="context">The authenticated request.</param>
    /// <returns>The raw access token; never log it.</returns>
    /// <exception cref="UnauthorizedAccessException">The request has no bearer credentials.</exception>
    public static string OriginToken(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        const string Scheme = "Bearer ";
        var header = context.Request.Headers.Authorization.ToString();
        var token = header.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase) ? header[Scheme.Length..].Trim() : string.Empty;
        return token.Length == 0
            ? throw new UnauthorizedAccessException("A validated bearer token is required.")
            : token;
    }

    /// <summary>Reads the single validated organization alias from the current principal.</summary>
    /// <param name="context">The authenticated request.</param>
    /// <returns>The selected organization alias.</returns>
    /// <exception cref="InvalidOperationException">The membership is absent, malformed or ambiguous.</exception>
    public static string SelectedOrganizationAlias(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            var organizations = context.User.GetOrganizations();
            return organizations.Count == 1 && !string.IsNullOrWhiteSpace(organizations[0].Alias)
                ? organizations[0].Alias
                : throw new InvalidOperationException("Exactly one validated organization alias is required.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("The organization membership claim is malformed.", exception);
        }
    }

    /// <summary>Reads the single validated organization identifier from the current principal.</summary>
    /// <param name="context">The authenticated request.</param>
    /// <returns>The canonical organization UUID.</returns>
    /// <exception cref="InvalidOperationException">The membership is absent, malformed or ambiguous.</exception>
    public static string SelectedOrganizationId(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            var organizations = context.User.GetOrganizations();
            return organizations.Count == 1 && Guid.TryParseExact(organizations[0].Id, "D", out var parsed)
                ? parsed.ToString("D")
                : throw new InvalidOperationException("Exactly one validated organization UUID is required.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("The organization membership claim is malformed.", exception);
        }
    }

    internal static HttpContext RequireValidatedContext(IHttpContextAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        var http = accessor.HttpContext ?? throw new InvalidOperationException("A validated commerce request is required.");
        return http.User.Identity?.IsAuthenticated == true
            ? http
            : throw new UnauthorizedAccessException("The request has no validated principal.");
    }

    private static KeycloakServiceOptions Keycloak(CommerceAuthOptions options, string audience) => new()
    {
        ServerUrl = options.ServerUrl,
        Realm = options.Realm,
        Audience = audience,
        PublicIssuer = options.PublicIssuer,
        Transport = options.Transport,
    };

    private static void AddStaffPolicies(AuthorizationBuilder authorization)
    {
        _ = authorization.AddPolicy(OrdersReadPolicy, policy => policy.RequireAuthenticatedUser().RequireRealmRoles(OrdersReadPermission));
        _ = authorization.AddPolicy(OrdersDecidePolicy, policy => policy.RequireAuthenticatedUser().RequireRealmRoles(OrdersDecidePermission));
        _ = authorization.AddPolicy(PlacementRecordPolicy, policy => policy.RequireAuthenticatedUser().RequireRealmRoles(PlacementRecordPermission));
    }

    private static void AddOutboundCredentials(IServiceCollection services, CommerceAuthOptions options)
    {
        var endpoint = options.TokenEndpoint;
        var clientId = options.ClientId;
        var clientSecret = options.ClientSecret;
        if (endpoint is null || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException("Commerce:Identity:TokenEndpoint, ClientId and ClientSecret are required for outbound commerce credentials.");
        }

        _ = services.AddHttpContextAccessor();
        _ = services.AddOAuthTokenClient(new OAuthClientOptions
        {
            TokenEndpoint = endpoint,
            ClientId = clientId,
            ClientSecret = clientSecret,
            Transport = options.Transport,
        });
    }

    private static Uri? OptionalUri(IConfigurationSection section, string name)
    {
        var value = section[name];
        return value switch
        {
            _ when string.IsNullOrWhiteSpace(value) => null,
            _ when Uri.TryCreate(value, UriKind.Absolute, out var uri) => uri,
            _ => throw new InvalidOperationException("Commerce:Identity:" + name + " must be an absolute URI."),
        };
    }

    private static IdentityTransport OptionalTransport(IConfigurationSection section)
    {
        var value = section[nameof(CommerceAuthOptions.Transport)];
        return value switch
        {
            _ when string.IsNullOrWhiteSpace(value) => IdentityTransport.Https,
            _ when Enum.TryParse<IdentityTransport>(value, ignoreCase: true, out var transport) && Enum.IsDefined(transport) => transport,
            _ => throw new InvalidOperationException("Commerce:Identity:Transport is invalid."),
        };
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
