using FastEndpoints;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Web.BFF.Services;

namespace Web.BFF.Endpoints;

public class SwitchOrgRequest
{
    public string Id { get; set; } = string.Empty;
}

[Authorize]
public class SwitchOrganizationEndpoint : Endpoint<SwitchOrgRequest>
{
    private readonly ITokenExchangeService _exchangeService;
    public SwitchOrganizationEndpoint(ITokenExchangeService exchangeService) => _exchangeService = exchangeService;

    public override void Configure()
    {
        Put("/auth/switch-organization");
    }

    public override async Task HandleAsync(SwitchOrgRequest req, CancellationToken ct)
    {
        var auth = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(auth) || !auth.StartsWith("Bearer "))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var subjectToken = auth.Substring("Bearer ".Length).Trim();
        var result = await _exchangeService.SwitchOrganizationAsync(subjectToken, req.Id, ct);
        HttpContext.Response.StatusCode = StatusCodes.Status200OK;
        await HttpContext.Response.WriteAsJsonAsync(new { access_token = result.AccessToken, expires_at = result.ExpiresAt }, cancellationToken: ct);
    }
}
