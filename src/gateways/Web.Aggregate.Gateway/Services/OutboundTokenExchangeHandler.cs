using System.Net.Http.Headers;
using SharedKernel.Infrastructure.Auth;

namespace Web.Aggregate.Gateway.Services;

internal sealed class OutboundTokenExchangeHandler : DelegatingHandler
{
    private const string TenantIdHeader = "X-TenantId";
    private const string TenantDbStrategyHeader = "X-Tenant-DbStrategy";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOutboundSecurityContextFactory _securityContextFactory;
    private readonly string _audience;

    public OutboundTokenExchangeHandler(
        IHttpContextAccessor httpContextAccessor,
        IOutboundSecurityContextFactory securityContextFactory,
        string audience)
    {
        _httpContextAccessor = httpContextAccessor;
        _securityContextFactory = securityContextFactory;
        _audience = audience;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpContext? httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        OutboundSecurityContext outbound = await _securityContextFactory.CreateAsync(
            httpContext,
            _audience,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(outbound.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", outbound.AccessToken);
        }

        CopyValueIfPresent(request, TenantIdHeader, outbound.TenantId);
        CopyValueIfPresent(request, TenantDbStrategyHeader, outbound.TenantDbStrategy);

        return await base.SendAsync(request, cancellationToken);
    }

    private static void CopyValueIfPresent(HttpRequestMessage request, string headerName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        request.Headers.Remove(headerName);
        request.Headers.TryAddWithoutValidation(headerName, value);
    }
}
