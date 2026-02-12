using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.BFF.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly Services.ITokenExchangeService _exchangeService;

    public AuthController(Services.ITokenExchangeService exchangeService)
    {
        _exchangeService = exchangeService;
    }

    [HttpPut("switch-organization")]
    [Authorize]
    public async Task<IActionResult> SwitchOrganization([FromBody] SwitchOrgRequest req)
    {
        var auth = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(auth) || !auth.StartsWith("Bearer ")) return Unauthorized();
        var subjectToken = auth.Substring("Bearer ".Length).Trim();

        var result = await _exchangeService.SwitchOrganizationAsync(subjectToken, req.Id, HttpContext.RequestAborted);
        return Ok(new { access_token = result.AccessToken, expires_at = result.ExpiresAt });
    }

    public record SwitchOrgRequest(string Id);
}
