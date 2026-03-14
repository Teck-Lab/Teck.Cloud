// <copyright file="KeycloakTenantIdentityProvisioningService.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using Customer.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Customer.Infrastructure.Identity;

/// <summary>
/// Provisions and removes tenant organizations in Keycloak.
/// </summary>
public sealed class KeycloakTenantIdentityProvisioningService : ITenantIdentityProvisioningService
{
    private static readonly Action<ILogger, string, int, string, Exception?> DeleteOrganizationFailureLog =
        LoggerMessage.Define<string, int, string>(
            LogLevel.Warning,
            new EventId(1001, nameof(DeleteOrganizationAsync)),
            "Failed to delete Keycloak organization {OrganizationId}. Status={StatusCode}; Body={Body}");

    private static readonly Action<ILogger, string, string, string, string, string, bool, Exception?> AdminTokenConfigurationResolvedLog =
        LoggerMessage.Define<string, string, string, string, string, bool>(
            LogLevel.Information,
            new EventId(1002, nameof(BuildAdminContextAsync)),
            "Resolved Keycloak admin token configuration. BaseUrl={BaseUrl} (from {BaseUrlKey}), Realm={RealmSource}, ClientId={ClientIdSource}, ClientSecretSource={ClientSecretSource}, ClientSecretConfigured={ClientSecretConfigured}");

    private readonly IHttpClientFactory httpClientFactory;
    private readonly IConfiguration configuration;
    private readonly ILogger<KeycloakTenantIdentityProvisioningService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeycloakTenantIdentityProvisioningService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public KeycloakTenantIdentityProvisioningService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<KeycloakTenantIdentityProvisioningService> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.configuration = configuration;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> CreateOrganizationAsync(
        string tenantIdentifier,
        string tenantName,
        CancellationToken cancellationToken)
    {
        CreateOrganizationInput request = CreateOrganizationInput.From(tenantIdentifier, tenantName);
        AdminContext adminContext = await this.BuildAdminContextAsync(cancellationToken).ConfigureAwait(false);
        using HttpClient client = this.CreateAuthorizedClient(adminContext);
        OperationContext operationContext = new() { Admin = adminContext, Client = client };
        using HttpResponseMessage createResponse = await this.CreateOrganizationCoreAsync(operationContext, request, cancellationToken).ConfigureAwait(false);
        ResolveLookupInput resolveLookupInput = new() { CreateResponse = createResponse, Request = request };
        return await this.ResolveOrganizationIdWithLookupAsync(operationContext, resolveLookupInput, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteOrganizationAsync(string organizationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return;
        }

        AdminContext adminContext = await this.BuildAdminContextAsync(cancellationToken).ConfigureAwait(false);
        using HttpClient client = this.CreateAuthorizedClient(adminContext);
        OperationContext operationContext = new() { Admin = adminContext, Client = client };
        using HttpResponseMessage deleteResponse = await this.DeleteOrganizationCoreAsync(operationContext, organizationId, cancellationToken).ConfigureAwait(false);
        await this.LogDeleteFailureIfAnyAsync(deleteResponse, organizationId, cancellationToken).ConfigureAwait(false);
    }

    private HttpClient CreateAuthorizedClient(AdminContext adminContext)
    {
        HttpClient client = this.httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminContext.AccessToken);
        return client;
    }

    private async Task<AdminContext> BuildAdminContextAsync(CancellationToken cancellationToken)
    {
        AdminConfiguration adminConfiguration = this.GetRequiredAdminConfiguration();
        this.LogAdminConfiguration(adminConfiguration);
        AdminContext adminContext = new()
        {
            BaseUrl = adminConfiguration.BaseUrl.Value.TrimEnd('/'),
            Realm = adminConfiguration.Realm.Value,
            ClientId = adminConfiguration.ClientId.Value,
            ClientSecret = adminConfiguration.ClientSecret.Value,
        };

        adminContext.AccessToken = await this.GetAdminAccessTokenAsync(adminContext, cancellationToken).ConfigureAwait(false);
        return adminContext;
    }

    private AdminConfiguration GetRequiredAdminConfiguration()
    {
        return new AdminConfiguration
        {
            BaseUrl = this.GetRequiredConfigEntryWithFallback("Keycloak:auth-server-url", "Keycloak:AuthServerUrl"),
            Realm = this.GetRequiredConfigEntry("Keycloak:realm"),
            ClientId = this.GetRequiredConfigEntry("Keycloak:resource"),
            ClientSecret = this.GetRequiredConfigEntry("Keycloak:credentials:secret"),
        };
    }

    private void LogAdminConfiguration(AdminConfiguration adminConfiguration)
    {
        string realmSource = $"{adminConfiguration.Realm.Value} (from {adminConfiguration.Realm.KeyUsed})";
        string clientIdSource = $"{adminConfiguration.ClientId.Value} (from {adminConfiguration.ClientId.KeyUsed})";
        AdminTokenConfigurationResolvedLog(
            this.logger,
            adminConfiguration.BaseUrl.Value,
            adminConfiguration.BaseUrl.KeyUsed,
            realmSource,
            clientIdSource,
            adminConfiguration.ClientSecret.KeyUsed,
            !string.IsNullOrWhiteSpace(adminConfiguration.ClientSecret.Value),
            null);
    }

    [RequiresDynamicCode("Calls System.Net.Http.Json.JsonContent.Create<T>(T, MediaTypeHeaderValue, JsonSerializerOptions)")]
    [RequiresUnreferencedCode("Calls System.Net.Http.Json.JsonContent.Create<T>(T, MediaTypeHeaderValue, JsonSerializerOptions)")]
    private async Task<HttpResponseMessage> CreateOrganizationCoreAsync(
        OperationContext operationContext,
        CreateOrganizationInput request,
        CancellationToken cancellationToken)
    {
        _ = this.logger;
        var payload = new { name = request.TenantName, alias = request.TenantIdentifier, enabled = true };
        using var createContent = System.Net.Http.Json.JsonContent.Create(payload);
        Uri requestUri = KeycloakPrimitives.BuildOrganizationUri(operationContext.Admin);
        HttpResponseMessage response = await operationContext.Client.PostAsync(requestUri, createContent, cancellationToken).ConfigureAwait(false);
        await KeycloakPrimitives.ThrowIfCreateFailedAsync(response, request.TenantIdentifier, cancellationToken).ConfigureAwait(false);
        return response;
    }

    private async Task<HttpResponseMessage> DeleteOrganizationCoreAsync(
        OperationContext operationContext,
        string organizationId,
        CancellationToken cancellationToken)
    {
        _ = this.logger;
        Uri requestUri = KeycloakPrimitives.BuildOrganizationUriWithId(operationContext.Admin, organizationId);
        return await operationContext.Client.DeleteAsync(requestUri, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> ResolveOrganizationIdWithLookupAsync(
        OperationContext operationContext,
        ResolveLookupInput resolveLookupInput,
        CancellationToken cancellationToken)
    {
        string createdOrganizationId = KeycloakPrimitives.GetOrganizationIdFromLocation(resolveLookupInput.CreateResponse.Headers.Location);
        if (!string.IsNullOrWhiteSpace(createdOrganizationId))
        {
            return createdOrganizationId;
        }

        using HttpResponseMessage lookupResponse = await this.GetLookupResponseAsync(operationContext, resolveLookupInput.Request, cancellationToken).ConfigureAwait(false);
        string lookupBody = await lookupResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        string resolvedId = KeycloakPrimitives.ResolveOrganizationId(lookupBody, resolveLookupInput.Request.TenantIdentifier, resolveLookupInput.Request.TenantName);
        return !string.IsNullOrWhiteSpace(resolvedId)
            ? resolvedId
            : throw new InvalidOperationException("Organization was created but could not resolve its identifier.");
    }

    private async Task<HttpResponseMessage> GetLookupResponseAsync(
        OperationContext operationContext,
        CreateOrganizationInput request,
        CancellationToken cancellationToken)
    {
        _ = this.logger;
        Uri lookupUri = KeycloakPrimitives.BuildOrganizationLookupUri(operationContext.Admin, request.TenantIdentifier);
        HttpResponseMessage lookupResponse = await operationContext.Client.GetAsync(lookupUri, cancellationToken).ConfigureAwait(false);
        lookupResponse.EnsureSuccessStatusCode();
        return lookupResponse;
    }

    private async Task<string> GetAdminAccessTokenAsync(AdminContext adminContext, CancellationToken cancellationToken)
    {
        using HttpClient client = this.httpClientFactory.CreateClient();
        TokenFlowContext tokenFlowContext = new(client, adminContext, cancellationToken);
        TokenAttempt postAttempt = await this.RequestTokenUsingClientSecretPostAsync(tokenFlowContext).ConfigureAwait(false);
        if (postAttempt.IsSuccess)
        {
            return KeycloakPrimitives.ExtractAccessToken(postAttempt.Body);
        }

        return await this.GetTokenWithFallbackOrThrowAsync(tokenFlowContext, postAttempt).ConfigureAwait(false);
    }

    private async Task<string> GetTokenWithFallbackOrThrowAsync(TokenFlowContext tokenFlowContext, TokenAttempt postAttempt)
    {
        _ = this.logger;
        if (!this.ShouldRetryWithClientSecretBasic(postAttempt))
        {
            throw new InvalidOperationException($"Failed to obtain Keycloak admin token. Status={(int)postAttempt.StatusCode}; Body={postAttempt.Body}");
        }

        TokenAttempt basicAttempt = await this.RequestTokenUsingClientSecretBasicAsync(tokenFlowContext).ConfigureAwait(false);
        return this.ResolveTokenFromBasicAttemptOrThrow(postAttempt, basicAttempt);
    }

    private async Task<TokenAttempt> RequestTokenUsingClientSecretPostAsync(TokenFlowContext tokenFlowContext)
    {
        _ = this.logger;
        using var content = KeycloakPrimitives.CreateTokenRequestContent(tokenFlowContext.AdminContext.ClientId, tokenFlowContext.AdminContext.ClientSecret);
        using HttpResponseMessage response = await tokenFlowContext.Client.PostAsync(tokenFlowContext.TokenUri, content, tokenFlowContext.CancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(tokenFlowContext.CancellationToken).ConfigureAwait(false);
        return new TokenAttempt(response.IsSuccessStatusCode, response.StatusCode, body);
    }

    private async Task<TokenAttempt> RequestTokenUsingClientSecretBasicAsync(TokenFlowContext tokenFlowContext)
    {
        _ = this.logger;
        using var content = KeycloakPrimitives.CreateTokenRequestContentWithoutClientCredentials();
        tokenFlowContext.Client.DefaultRequestHeaders.Authorization = KeycloakPrimitives.CreateBasicClientCredentials(tokenFlowContext.AdminContext.ClientId, tokenFlowContext.AdminContext.ClientSecret);
        using HttpResponseMessage response = await tokenFlowContext.Client.PostAsync(tokenFlowContext.TokenUri, content, tokenFlowContext.CancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(tokenFlowContext.CancellationToken).ConfigureAwait(false);
        return new TokenAttempt(response.IsSuccessStatusCode, response.StatusCode, body);
    }

    private bool ShouldRetryWithClientSecretBasic(TokenAttempt postAttempt)
    {
        _ = this.logger;
        return postAttempt.StatusCode == HttpStatusCode.Unauthorized &&
               KeycloakPrimitives.IsUnauthorizedClientError(postAttempt.Body);
    }

    private string ResolveTokenFromBasicAttemptOrThrow(TokenAttempt postAttempt, TokenAttempt basicAttempt)
    {
        _ = this.logger;
        if (basicAttempt.IsSuccess)
        {
            return KeycloakPrimitives.ExtractAccessToken(basicAttempt.Body);
        }

        throw new InvalidOperationException(
            $"Failed to obtain Keycloak admin token. " +
            $"post:Status={(int)postAttempt.StatusCode}; Body={postAttempt.Body}; " +
            $"basic:Status={(int)basicAttempt.StatusCode}; Body={basicAttempt.Body}");
    }

    private async Task LogDeleteFailureIfAnyAsync(HttpResponseMessage deleteResponse, string organizationId, CancellationToken cancellationToken)
    {
        if (deleteResponse.IsSuccessStatusCode || deleteResponse.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        string body = await deleteResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        DeleteOrganizationFailureLog(this.logger, organizationId, (int)deleteResponse.StatusCode, body, null);
    }

    private (string Value, string KeyUsed) GetRequiredConfigEntry(string primaryKey)
    {
        string? value = this.configuration[primaryKey];
        return !string.IsNullOrWhiteSpace(value)
            ? (value, primaryKey)
            : throw new InvalidOperationException($"Keycloak configuration value '{primaryKey}' is missing.");
    }

    private (string Value, string KeyUsed) GetRequiredConfigEntryWithFallback(string primaryKey, string fallbackKey)
    {
        string? primaryValue = this.configuration[primaryKey];
        if (!string.IsNullOrWhiteSpace(primaryValue))
        {
            return (primaryValue, primaryKey);
        }

        string? fallbackValue = this.configuration[fallbackKey];
        return !string.IsNullOrWhiteSpace(fallbackValue)
            ? (fallbackValue, fallbackKey)
            : throw new InvalidOperationException($"Keycloak configuration value '{primaryKey}' is missing.");
    }

    private static class KeycloakPrimitives
    {
        public static async Task ThrowIfCreateFailedAsync(
            HttpResponseMessage createResponse,
            string tenantIdentifier,
            CancellationToken cancellationToken)
        {
            if (createResponse.StatusCode == HttpStatusCode.Conflict)
            {
                throw new InvalidOperationException($"Organization already exists for tenant identifier '{tenantIdentifier}'.");
            }

            if (!createResponse.IsSuccessStatusCode)
            {
                string body = await createResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException($"Failed to create Keycloak organization. Status={(int)createResponse.StatusCode}; Body={body}");
            }
        }

        public static string BuildOrganizationPath(AdminContext adminContext)
        {
            return $"/admin/realms/{adminContext.Realm}/organizations";
        }

        public static Uri BuildOrganizationUri(AdminContext adminContext)
        {
            string organizationPath = BuildOrganizationPath(adminContext);
            return BuildBaseUri(adminContext.BaseUrl, organizationPath);
        }

        public static string BuildOrganizationPathWithId(AdminContext adminContext, string organizationId)
        {
            string basePath = BuildOrganizationPath(adminContext);
            string escapedOrganizationId = Uri.EscapeDataString(organizationId);
            return $"{basePath}/{escapedOrganizationId}";
        }

        public static Uri BuildOrganizationUriWithId(AdminContext adminContext, string organizationId)
        {
            string organizationPath = BuildOrganizationPathWithId(adminContext, organizationId);
            return BuildBaseUri(adminContext.BaseUrl, organizationPath);
        }

        public static string BuildOrganizationLookupPath(AdminContext adminContext, string tenantIdentifier)
        {
            string basePath = BuildOrganizationPath(adminContext);
            string escapedIdentifier = Uri.EscapeDataString(tenantIdentifier);
            return $"{basePath}?search={escapedIdentifier}";
        }

        public static Uri BuildOrganizationLookupUri(AdminContext adminContext, string tenantIdentifier)
        {
            string organizationLookupPath = BuildOrganizationLookupPath(adminContext, tenantIdentifier);
            return BuildBaseUri(adminContext.BaseUrl, organizationLookupPath);
        }

        public static string BuildTokenPath(AdminContext adminContext)
        {
            return $"/realms/{adminContext.Realm}/protocol/openid-connect/token";
        }

        public static Uri BuildTokenUri(AdminContext adminContext)
        {
            string tokenPath = BuildTokenPath(adminContext);
            return BuildBaseUri(adminContext.BaseUrl, tokenPath);
        }

        public static FormUrlEncodedContent CreateTokenRequestContent(string clientId, string clientSecret)
        {
            return new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
            ]);
        }

        public static FormUrlEncodedContent CreateTokenRequestContentWithoutClientCredentials()
        {
            return new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
            ]);
        }

        public static AuthenticationHeaderValue CreateBasicClientCredentials(string clientId, string clientSecret)
        {
            string rawCredentials = $"{clientId}:{clientSecret}";
            byte[] rawCredentialBytes = System.Text.Encoding.UTF8.GetBytes(rawCredentials);
            string encodedCredentials = Convert.ToBase64String(rawCredentialBytes);
            return new AuthenticationHeaderValue("Basic", encodedCredentials);
        }

        public static bool IsUnauthorizedClientError(string tokenResponseBody)
        {
            using System.Text.Json.JsonDocument tokenJson = System.Text.Json.JsonDocument.Parse(tokenResponseBody);
            if (!tokenJson.RootElement.TryGetProperty("error", out System.Text.Json.JsonElement errorElement))
            {
                return false;
            }

            string? errorValue = errorElement.GetString();
            return string.Equals(errorValue, "unauthorized_client", StringComparison.OrdinalIgnoreCase);
        }

        public static string ExtractAccessToken(string tokenResponseBody)
        {
            using System.Text.Json.JsonDocument tokenJson = System.Text.Json.JsonDocument.Parse(tokenResponseBody);
            if (!tokenJson.RootElement.TryGetProperty("access_token", out System.Text.Json.JsonElement accessTokenElement))
            {
                throw new InvalidOperationException("Keycloak token response did not contain access_token.");
            }

            string? tokenValue = accessTokenElement.GetString();
            if (!string.IsNullOrWhiteSpace(tokenValue))
            {
                return tokenValue;
            }

            throw new InvalidOperationException("Keycloak token response did not contain access_token.");
        }

        public static string GetOrganizationIdFromLocation(Uri? location)
        {
            if (location is null)
            {
                return string.Empty;
            }

            string[] segments = location.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length == 0 ? string.Empty : segments[^1];
        }

        public static string GetStringPropertyValue(System.Text.Json.JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out System.Text.Json.JsonElement propertyElement))
            {
                return string.Empty;
            }

            string? value = propertyElement.GetString();
            return value ?? string.Empty;
        }

        public static bool IsOrganizationMatch(System.Text.Json.JsonElement element, string tenantIdentifier, string tenantName)
        {
            string alias = GetStringPropertyValue(element, "alias");
            string name = GetStringPropertyValue(element, "name");
            return string.Equals(alias, tenantIdentifier, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, tenantName, StringComparison.OrdinalIgnoreCase);
        }

        public static string ResolveOrganizationId(string json, string tenantIdentifier, string tenantName)
        {
            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json);
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array
                ? FindOrganizationId(document.RootElement, tenantIdentifier, tenantName)
                : string.Empty;
        }

        private static Uri BuildBaseUri(string baseUrl, string relativePath)
        {
            return new($"{baseUrl.TrimEnd('/')}{relativePath}", UriKind.Absolute);
        }

        private static string FindOrganizationId(System.Text.Json.JsonElement organizations, string tenantIdentifier, string tenantName)
        {
            foreach (System.Text.Json.JsonElement element in organizations.EnumerateArray())
            {
                if (!IsOrganizationMatch(element, tenantIdentifier, tenantName))
                {
                    continue;
                }

                string id = GetStringPropertyValue(element, "id");
                if (!string.IsNullOrWhiteSpace(id))
                {
                    return id;
                }
            }

            return string.Empty;
        }
    }

    private sealed class AdminContext
    {
        public string AccessToken { get; set; } = string.Empty;

        public string BaseUrl { get; init; } = string.Empty;

        public string ClientId { get; init; } = string.Empty;

        public string ClientSecret { get; init; } = string.Empty;

        public string Realm { get; init; } = string.Empty;
    }

    private sealed class AdminConfiguration
    {
        public (string Value, string KeyUsed) BaseUrl { get; init; }

        public (string Value, string KeyUsed) ClientId { get; init; }

        public (string Value, string KeyUsed) ClientSecret { get; init; }

        public (string Value, string KeyUsed) Realm { get; init; }
    }

    private sealed class CreateOrganizationInput
    {
        public string TenantIdentifier { get; init; } = string.Empty;

        public string TenantName { get; init; } = string.Empty;

        public static CreateOrganizationInput From(string tenantIdentifier, string tenantName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantIdentifier);
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantName);
            return new CreateOrganizationInput { TenantIdentifier = tenantIdentifier, TenantName = tenantName };
        }
    }

    private sealed class OperationContext
    {
        public AdminContext Admin { get; init; } = new();

        public HttpClient Client { get; init; } = new();
    }

    private sealed class TokenAttempt
    {
        public TokenAttempt(bool isSuccess, HttpStatusCode statusCode, string body)
        {
            this.IsSuccess = isSuccess;
            this.StatusCode = statusCode;
            this.Body = body;
        }

        public string Body { get; }

        public bool IsSuccess { get; }

        public HttpStatusCode StatusCode { get; }
    }

    private sealed class TokenFlowContext
    {
        public TokenFlowContext(HttpClient client, AdminContext adminContext, CancellationToken cancellationToken)
        {
            this.Client = client;
            this.AdminContext = adminContext;
            this.CancellationToken = cancellationToken;
            this.TokenUri = KeycloakPrimitives.BuildTokenUri(adminContext);
        }

        public AdminContext AdminContext { get; }

        public CancellationToken CancellationToken { get; }

        public HttpClient Client { get; }

        public Uri TokenUri { get; }
    }

    private sealed class ResolveLookupInput
    {
        public HttpResponseMessage CreateResponse { get; init; } = new();

        public CreateOrganizationInput Request { get; init; } = new();
    }
}
