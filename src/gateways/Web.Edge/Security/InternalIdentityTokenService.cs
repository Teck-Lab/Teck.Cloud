using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Web.Edge.Security;

public interface IInternalIdentityTokenService
{
    string? CreateToken(ClaimsPrincipal user);
}

public sealed class InternalIdentityTokenService : IInternalIdentityTokenService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalIdentityTokenService> _logger;

    public InternalIdentityTokenService(IConfiguration configuration, ILogger<InternalIdentityTokenService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string? CreateToken(ClaimsPrincipal user)
    {
        var signingKey = _configuration["EdgeTrust:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            _logger.LogWarning("EdgeTrust:SigningKey is not configured; internal identity token will not be issued.");
            return null;
        }

        var issuer = _configuration["EdgeTrust:Issuer"] ?? "teck-edge";
        var audience = _configuration["EdgeTrust:Audience"] ?? "teck-web-bff-internal";
        var lifetimeSeconds = int.TryParse(_configuration["EdgeTrust:LifetimeSeconds"], out var seconds) ? seconds : 120;

        var now = DateTime.UtcNow;
        var claims = user.Claims
            .Where(claim =>
                !string.Equals(claim.Type, JwtRegisteredClaimNames.Exp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(claim.Type, JwtRegisteredClaimNames.Nbf, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(claim.Type, JwtRegisteredClaimNames.Iat, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(claim.Type, JwtRegisteredClaimNames.Aud, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(claim.Type, JwtRegisteredClaimNames.Iss, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!claims.Any(claim => string.Equals(claim.Type, ClaimTypes.NameIdentifier, StringComparison.OrdinalIgnoreCase) || string.Equals(claim.Type, "sub", StringComparison.OrdinalIgnoreCase)))
        {
            var subject = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
            if (!string.IsNullOrWhiteSpace(subject))
            {
                claims.Add(new Claim("sub", subject));
            }
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = now.AddSeconds(lifetimeSeconds),
            NotBefore = now.AddSeconds(-5),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials,
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }
}
