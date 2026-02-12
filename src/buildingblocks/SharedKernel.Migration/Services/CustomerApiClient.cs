using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using SharedKernel.Migration.Models;

namespace SharedKernel.Migration.Services;

/// <summary>
/// Client for communicating with the Customer API service.
/// </summary>
public sealed class CustomerApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CustomerApiClient> _logger;
    private const string HttpClientName = "CustomerApi";

    public CustomerApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<CustomerApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Updates the migration status for a tenant's service.
    /// </summary>
    public async Task<bool> UpdateMigrationStatusAsync(
        string tenantId,
        string serviceName,
        MigrationStatus status,
        string? lastMigrationVersion = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient(HttpClientName);

            var request = new UpdateMigrationStatusRequest
            {
                Status = status.ToString(),
                LastMigrationVersion = lastMigrationVersion,
                ErrorMessage = errorMessage,
            };

            var response = await httpClient.PutAsJsonAsync(
                $"api/v1/tenants/{tenantId}/services/{serviceName}/migration-status",
                request,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Updated migration status for tenant {TenantId}, service {ServiceName} to {Status}",
                    tenantId, serviceName, status);
                return true;
            }

            _logger.LogWarning(
                "Failed to update migration status for tenant {TenantId}, service {ServiceName}. Status: {StatusCode}",
                tenantId, serviceName, response.StatusCode);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error updating migration status for tenant {TenantId}, service {ServiceName}",
                tenantId, serviceName);
            return false;
        }
    }

    /// <summary>
    /// Gets the database info for a tenant's service.
    /// </summary>
    public async Task<ServiceDatabaseInfo?> GetServiceDatabaseInfoAsync(
        string tenantId,
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient(HttpClientName);

            var response = await httpClient.GetAsync(
                $"api/v1/tenants/{tenantId}/services/{serviceName}/database-info",
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var info = await response.Content.ReadFromJsonAsync<ServiceDatabaseInfo>(
                    cancellationToken: cancellationToken);

                _logger.LogInformation(
                    "Retrieved database info for tenant {TenantId}, service {ServiceName}",
                    tenantId, serviceName);

                return info;
            }

            _logger.LogWarning(
                "Failed to get database info for tenant {TenantId}, service {ServiceName}. Status: {StatusCode}",
                tenantId, serviceName, response.StatusCode);

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error getting database info for tenant {TenantId}, service {ServiceName}",
                tenantId, serviceName);
            return null;
        }
    }

    private sealed record UpdateMigrationStatusRequest
    {
        public required string Status { get; init; }
        public string? LastMigrationVersion { get; init; }
        public string? ErrorMessage { get; init; }
    }
}

/// <summary>
/// Service database information from Customer API.
/// </summary>
public sealed record ServiceDatabaseInfo
{
    public required string VaultWritePath { get; init; }
    public string? VaultReadPath { get; init; }
    public required bool HasSeparateReadDatabase { get; init; }
}
