using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SharedKernel.Core.Pricing;

namespace SharedKernel.Infrastructure.MultiTenant
{
    /// <summary>
    /// Lightweight tenant store that resolves tenant details directly from request context.
    /// This store avoids external calls and is intended for header-driven gateway flows.
    /// </summary>
    public sealed class HeaderTenantStore : IMultiTenantStore<TenantDetails>
    {
        private const string TenantDbStrategyHeaderName = "X-Tenant-DbStrategy";

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly TeckCloudMultiTenancyOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="HeaderTenantStore"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">HTTP context accessor.</param>
        /// <param name="options">Multi-tenancy options.</param>
        public HeaderTenantStore(
            IHttpContextAccessor httpContextAccessor,
            IOptions<TeckCloudMultiTenancyOptions> options)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc/>
        public Task<IEnumerable<TenantDetails>> GetAllAsync()
        {
            return Task.FromResult(Enumerable.Empty<TenantDetails>());
        }

        /// <inheritdoc/>
        public Task<IEnumerable<TenantDetails>> GetAllAsync(int take, int skip)
        {
            _ = take;
            _ = skip;
            return Task.FromResult(Enumerable.Empty<TenantDetails>());
        }

        /// <inheritdoc/>
        public Task<TenantDetails?> GetByIdentifierAsync(string identifier)
        {
            return TryGetByIdentifierAsync(identifier);
        }

        /// <inheritdoc/>
        public Task<TenantDetails?> GetAsync(string id)
        {
            return TryGetAsync(id);
        }

        /// <inheritdoc/>
        public Task<bool> AddAsync(TenantDetails tenantInfo)
        {
            return Task.FromResult(false);
        }

        /// <inheritdoc/>
        public Task<bool> RemoveAsync(string identifier)
        {
            return Task.FromResult(false);
        }

        /// <inheritdoc/>
        public Task<bool> UpdateAsync(TenantDetails tenantInfo)
        {
            return Task.FromResult(false);
        }

        /// <inheritdoc/>
        public Task<TenantDetails?> TryGetByIdentifierAsync(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return Task.FromResult<TenantDetails?>(null);
            }

            if (!MatchesHeaderTenant(identifier))
            {
                return Task.FromResult<TenantDetails?>(null);
            }

            return Task.FromResult<TenantDetails?>(BuildTenant(identifier, identifier));
        }

        /// <inheritdoc/>
        public Task<TenantDetails?> TryGetAsync(string id)
        {
            return TryGetByIdentifierAsync(id);
        }

        /// <inheritdoc/>
        public Task<TenantDetails?> TryGetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return TryGetByIdentifierAsync(id);
        }

        /// <inheritdoc/>
        public Task<TenantDetails?> TryGetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult<TenantDetails?>(null);
        }

        /// <inheritdoc/>
        public Task<bool> TryAddAsync(TenantDetails tenantInfo)
        {
            return Task.FromResult(false);
        }

        /// <inheritdoc/>
        public Task<bool> TryRemoveAsync(string identifier)
        {
            return Task.FromResult(false);
        }

        /// <inheritdoc/>
        public Task<bool> TryUpdateAsync(TenantDetails tenantInfo)
        {
            return Task.FromResult(false);
        }

        private bool MatchesHeaderTenant(string requestedTenantId)
        {
            HttpContext? httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return true;
            }

            string tenantHeaderName = _options.TenantIdHeaderName;
            if (!httpContext.Request.Headers.TryGetValue(tenantHeaderName, out var headerValue))
            {
                return true;
            }

            string resolvedHeaderTenant = headerValue.ToString();
            if (string.IsNullOrWhiteSpace(resolvedHeaderTenant))
            {
                return true;
            }

            return string.Equals(resolvedHeaderTenant, requestedTenantId, StringComparison.OrdinalIgnoreCase);
        }

        private TenantDetails BuildTenant(string id, string identifier)
        {
            string strategy = ResolveDatabaseStrategyFromHeader();

            return new TenantDetails
            {
                Id = id,
                Identifier = identifier,
                Name = identifier,
                IsActive = true,
                DatabaseStrategy = strategy,
                DatabaseProvider = string.Empty,
                Plan = string.Empty,
            };
        }

        private string ResolveDatabaseStrategyFromHeader()
        {
            HttpContext? httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return DatabaseStrategy.Shared.Name;
            }

            if (!httpContext.Request.Headers.TryGetValue(TenantDbStrategyHeaderName, out var strategyHeaderValue))
            {
                return DatabaseStrategy.Shared.Name;
            }

            string strategy = strategyHeaderValue.ToString();
            if (string.IsNullOrWhiteSpace(strategy))
            {
                return DatabaseStrategy.Shared.Name;
            }

            if (DatabaseStrategy.TryFromName(strategy, true, out var resolvedStrategy))
            {
                return resolvedStrategy.Name;
            }

            return DatabaseStrategy.Shared.Name;
        }
    }
}
