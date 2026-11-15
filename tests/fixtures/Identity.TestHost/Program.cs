using Duende.AccessTokenManagement.OpenIdConnect;
using Identity.TestHost.Contracts;
using Keycloak.AuthServices.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Infrastructure.Auth;
using SharedKernel.Infrastructure.MultiTenancy;
using SharedKernel.Persistence.Caching;
using SharedKernel.Persistence.Database;

namespace Identity.TestHost;

/// <summary>Test-only gateway/service processes; never a production business service.</summary>
public static class Program
{
    /// <summary>Runs the selected fixture role.</summary>
    /// <param name="args">Host configuration.</param>
    /// <returns>The process lifetime.</returns>
    /// <exception cref="InvalidOperationException">A required isolated fixture setting is missing.</exception>
    public static async Task Main(string[] args)
    {
        var builder = CreateBuilder(args);
        var identity = new Uri(builder.Configuration["Identity:ServerUrl"] ?? throw new InvalidOperationException("Identity server is required."));
        var realm = builder.Configuration["Identity:Realm"] ?? "teck-local";
        var issuer = builder.Configuration["Identity:PublicIssuer"] is { } configuredIssuer ? new Uri(configuredIssuer) : new Uri(identity, "realms/" + realm);
        var transport = Enum.Parse<IdentityTransport>(builder.Configuration["Identity:Transport"] ?? nameof(IdentityTransport.IsolatedDevelopment));
        var gateway = string.Equals(builder.Configuration["IDENTITY_ROLE"], "Gateway", StringComparison.Ordinal);
        var clientId = builder.Configuration["Identity:ClientId"] ?? "identity-gateway";
        var secret = builder.Configuration["Identity:ClientSecret"] ?? throw new InvalidOperationException("Fixture secret is required.");
        _ = builder.Services.AddPersistenceFoundation();
        _ = builder.Services.AddFoundationRedis(builder.Configuration.GetConnectionString("redis") ?? throw new InvalidOperationException("Redis Sentinel is required."));
        _ = builder.Services.AddFoundationKeycloakAdmin(
            new Keycloak.AuthServices.Sdk.Kiota.KeycloakAdminClientOptions
            {
                AuthServerUrl = identity.ToString(),
                Realm = realm,
                Resource = "tenant-reader",
                Credentials = new() { Secret = builder.Configuration["Identity:ReaderSecret"] ?? throw new InvalidOperationException("Reader secret is required.") },
            },
            transport);
        _ = builder.Services.AddKeycloakTenantStore();
        _ = builder.Services.AddFoundationKeycloakAuthentication(new KeycloakServiceOptions
        {
            ServerUrl = identity,
            PublicIssuer = issuer,
            Realm = realm,
            Audience = gateway ? clientId : "identity-service",
            Transport = transport,
        });
        if (gateway)
        {
            AddGateway(builder, identity, realm, secret, clientId, issuer, transport);
        }
        else
        {
            AddService(builder);
        }

        var app = builder.Build();
        await using var disposeApp = app.ConfigureAwait(false);
        _ = app.UseRouting();
        _ = app.UseAuthentication();
        _ = app.UseAuthorization();
        _ = app.UseMiddleware<OperationScopeMiddleware>();
        _ = app.MapGet("/healthz", () => "ready").AllowAnonymous();
        if (gateway)
        {
            GatewayEndpoints.Map(app);
        }
        else
        {
            var scope = app.Services.CreateAsyncScope();
            await using var disposeScope = scope.ConfigureAwait(false);
            scope.ServiceProvider.GetRequiredService<OperationContext>().Initialize(DataScope.Global);
            _ = await scope.ServiceProvider.GetRequiredService<ProbeWriteDbContext>().Database.EnsureCreatedAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false);
            _ = app.MapGrpcService<RecordService>().WithMetadata(OperationScope.Tenant);
            _ = app.MapGrpcService<AdminRecordService>();
        }

        await app.RunAsync().ConfigureAwait(false);
    }

    private static WebApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Native callback URLs can carry short-lived authorization codes; do not log their query strings.
        _ = builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
        return builder;
    }

    private static void AddService(WebApplicationBuilder builder)
    {
        var database = builder.Configuration.GetConnectionString("identity-db") ?? throw new InvalidOperationException("Fixture database is required.");
        _ = builder.Services.AddDbContext<ProbeWriteDbContext>(options => options.UseNpgsql(database));
        _ = builder.Services.AddDbContext<ProbeReadDbContext>(options => options.UseNpgsql(database));
        _ = builder.Services.AddGrpc();
        _ = builder.Services.AddKeycloakAssurance();
        _ = builder.Services.AddAuthorizationBuilder()
            .AddPolicy("admin-read", policy => policy.RequireAuthenticatedUser().RequireRealmRoles("platform-admin").RequireProtectedResource("records", "read").RequireKeycloakAuthorizedParty(builder.Configuration["Identity:TrustedAdminClient"] ?? "identity-admin-gateway"))
            .AddPolicy("records-read", policy => policy.RequireAuthenticatedUser().RequireOrganizationMembership().RequireProtectedResource("records", "read"))
            .AddPolicy("records-write", policy => policy.RequireAuthenticatedUser().RequireOrganizationMembership().RequireProtectedResource("records", "write"));
    }

    private static void AddGateway(WebApplicationBuilder builder, Uri identity, string realm, string secret, string clientId, Uri issuer, IdentityTransport transport)
    {
        _ = builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        }).AddCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
        }).AddOpenIdConnect(options =>
        {
            options.Authority = issuer.ToString();
            options.MetadataAddress = new Uri(identity, "realms/" + realm + "/.well-known/openid-configuration").ToString();
            options.ClientId = clientId;
            options.ClientSecret = secret;
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.UsePkce = true;
            options.RequireHttpsMetadata = transport == IdentityTransport.Https;
            options.BackchannelTimeout = TimeSpan.FromSeconds(20);
            options.BackchannelHttpHandler = new SocketsHttpHandler { AllowAutoRedirect = false };
            options.MapInboundClaims = false;
            options.ClaimActions.Remove("acr");
            options.SaveTokens = true;
            options.GetClaimsFromUserInfoEndpoint = false;
            options.Events.OnAuthorizationCodeReceived = context =>
            {
                context.HttpContext.RequestServices.GetRequiredService<PkceEvidence>().CodeVerifierPresent = !string.IsNullOrWhiteSpace(context.TokenEndpointRequest?.GetParameter("code_verifier"));
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToIdentityProvider = context =>
            {
                if (string.Equals(clientId, "identity-admin-gateway", StringComparison.Ordinal))
                {
                    context.ProtocolMessage.SetParameter("acr_values", "2");
                    context.ProtocolMessage.SetParameter("max_age", "0");
                    return Task.CompletedTask;
                }

                var organization = context.Properties.Items["organization"];
                if (organization is not ("alpha" or "beta"))
                {
                    throw new InvalidOperationException("Unknown fixture organization.");
                }

                context.ProtocolMessage.Scope += " organization:" + organization;
                return Task.CompletedTask;
            };
        });
        AddGatewayTransport(builder, identity, realm, secret, clientId, transport);
    }

    private static void AddGatewayTransport(WebApplicationBuilder builder, Uri identity, string realm, string secret, string clientId, IdentityTransport transport)
    {
        _ = builder.Services.AddKeycloakAssurance();
        _ = builder.Services.AddAuthorizationBuilder().AddPolicy("platform-admin", policy => policy.RequireAuthenticatedUser().RequireRealmRoles("platform-admin").RequireKeycloakAssurance("2", TimeSpan.FromSeconds(builder.Configuration.GetValue("Identity:MaximumAuthenticationAgeSeconds", 300))));
        _ = builder.Services.AddAntiforgery();
        _ = builder.Services.AddHttpContextAccessor();
        _ = builder.Services.AddOpenIdConnectAccessTokenManagement();
        _ = builder.Services.AddSingleton<PkceEvidence>();
        _ = builder.Services.AddScoped<IAccessTokenProvider, DelegatedTokenProvider>();
        _ = builder.Services.AddOAuthTokenClient(new OAuthClientOptions
        {
            TokenEndpoint = new Uri(identity, "realms/" + realm + "/protocol/openid-connect/token"),
            ClientId = clientId,
            ClientSecret = secret,
            Transport = transport,
        });
        _ = builder.Services.AddGrpcClient<AdminRecords.AdminRecordsClient>(options => options.Address = new Uri(builder.Configuration["Identity:DownstreamUrl"] ?? throw new InvalidOperationException("Downstream address is required.")))
            .AddOperationCredentials("identity-service", transport);
        _ = builder.Services.AddGrpcClient<Records.RecordsClient>(options => options.Address = new Uri(builder.Configuration["Identity:DownstreamUrl"] ?? throw new InvalidOperationException("Downstream address is required.")))
            .AddOperationCredentials("identity-service", transport);
    }
}
