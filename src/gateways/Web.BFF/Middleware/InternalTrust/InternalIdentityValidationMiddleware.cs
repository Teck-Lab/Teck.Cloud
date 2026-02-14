using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Web.BFF.Middleware.InternalTrust;

public sealed class InternalIdentityValidationMiddleware
{
    private const string InternalIdentityHeader = "X-Internal-Identity";

    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalIdentityValidationMiddleware> _logger;

    public InternalIdentityValidationMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<InternalIdentityValidationMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        StripClientSpoofableHeaders(context);

        var enforce = bool.TryParse(_configuration["EdgeTrust:Enforce"], out var enforceValue) && enforceValue;
        var isHealthRoute = context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);

        if (isHealthRoute)
        {
            await _next(context);
            return;
        }

        var token = context.Request.Headers[InternalIdentityHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            if (enforce)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Missing internal identity token.");
                return;
            }

            await _next(context);
            return;
        }

        if (!TryValidateToken(token, out var principal))
        {
            if (enforce)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid internal identity token.");
                return;
            }

            await _next(context);
            return;
        }

        context.Items["EdgeIdentityPrincipal"] = principal;

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            context.User = principal;
        }

        await _next(context);
    }

    private bool TryValidateToken(string token, out ClaimsPrincipal principal)
    {
        principal = new ClaimsPrincipal(new ClaimsIdentity());

        var signingKey = _configuration["EdgeTrust:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            _logger.LogWarning("EdgeTrust:SigningKey is not configured; cannot validate internal identity token.");
            return false;
        }

        var issuer = _configuration["EdgeTrust:Issuer"] ?? "teck-edge";
        var audience = _configuration["EdgeTrust:Audience"] ?? "teck-web-bff-internal";

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ClockSkew = TimeSpan.FromSeconds(15),
        };

        var handler = new JwtSecurityTokenHandler();

        try
        {
            principal = handler.ValidateToken(token, validationParameters, out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken ||
                !string.Equals(jwtToken.Header.Alg, SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Internal identity token uses an unexpected signing algorithm.");
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to validate internal identity token.");
            return false;
        }
    }

    private static void StripClientSpoofableHeaders(HttpContext context)
    {
        context.Request.Headers.Remove("X-TenantId");
        context.Request.Headers.Remove("X-Tenant-DbStrategy");
    }
}
