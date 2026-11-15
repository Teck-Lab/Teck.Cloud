using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Identity.Protocol;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Playwright;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.IntegrationTests;

internal sealed class KeycloakBrowser : IAsyncDisposable
{
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private readonly HttpClient _http = new();
    private readonly Uri _server;
    private readonly IdentityCallback _callback;
    private readonly string _redirect;
    private readonly string _password = RandomNumberGenerator.GetHexString(48);
    private readonly string _username = "mfa-" + Guid.NewGuid().ToString("N");
    private readonly List<string> _pageErrors = [];

    private KeycloakBrowser(IPlaywright playwright, IBrowser browser, IBrowserContext context, IPage page, Uri server, IdentityCallback callback, string redirect)
    {
        _playwright = playwright;
        _browser = browser;
        Context = context;
        Page = page;
        _server = server;
        _callback = callback;
        _redirect = redirect;
        page.PageError += (_, error) => _pageErrors.Add(error);
    }

    internal IBrowserContext Context { get; }

    internal IPage Page { get; }

    internal string UserId { get; private set; } = string.Empty;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _browser.DisposeAsync().ConfigureAwait(false);
        _playwright.Dispose();
        await _callback.DisposeAsync().ConfigureAwait(false);
    }

    internal static async Task<KeycloakBrowser> CreateAsync(KeycloakFixture fixture, string? requiredAction = null)
    {
        var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Channel = Environment.GetEnvironmentVariable("TECK_PLAYWRIGHT_CHANNEL"),
        }).ConfigureAwait(false);
        var context = await browser.NewContextAsync().ConfigureAwait(false);
        var page = await context.NewPageAsync().ConfigureAwait(false);
        page.SetDefaultTimeout(20000);
        var server = new UriBuilder(fixture.ServerAddress) { Host = "localhost" }.Uri;
        var callback = await IdentityCallback.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        var redirect = callback.RedirectUri;
        var result = new KeycloakBrowser(playwright, browser, context, page, server, callback, redirect);
        foreach (var client in new[] { "teck-web", "teck-admin" })
        {
            using var clients = await fixture.AdminAsync(HttpMethod.Get, "clients?clientId=" + client, body: null, TestContext.Current.CancellationToken).ConfigureAwait(false);
            var id = clients.RootElement[0].GetProperty("id").GetString();
            using var updated = await fixture.AdminAsync(HttpMethod.Put, "clients/" + id, new JsonObject { ["redirectUris"] = new JsonArray(redirect) }, TestContext.Current.CancellationToken).ConfigureAwait(false);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(fixture.ServerAddress, "admin/realms/teck-local/users"));
        request.Headers.Authorization = await fixture.AdminAuthorizationAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        var user = JsonSerializer.Serialize(new
        {
            username = result._username,
            enabled = true,
            email = result._username + "@example.test",
            emailVerified = true,
            firstName = "MFA",
            lastName = "Fixture",
            requiredActions = requiredAction is null ? Array.Empty<string>() : [requiredAction],
            credentials = new[] { new { type = "password", value = result._password, temporary = false } },
        });
        request.Content = new StringContent(user, Encoding.UTF8, "application/json");
        using var response = await result._http.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        result.UserId = response.Headers.Location!.Segments[^1];
        return result;
    }

    internal async Task<Dictionary<string, string>> AuthorizeAsync(string client = "teck-admin", string? maximumAge = null, string scope = "openid")
    {
        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var redirect = _redirect;
        var query = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["client_id"] = client,
            ["redirect_uri"] = redirect,
            ["response_type"] = "code",
            ["scope"] = scope,
            ["code_challenge"] = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(verifier))),
            ["code_challenge_method"] = "S256",

            // The admin's minimum must resist this explicit downgrade attempt.
            ["acr_values"] = "1",
        };
        if (maximumAge is not null)
        {
            query["max_age"] = maximumAge;
        }

        _ = await Page.GotoAsync(QueryHelpers.AddQueryString(new Uri(_server, "realms/teck-local/protocol/openid-connect/auth").AbsoluteUri, query)).ConfigureAwait(false);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = client,
            ["redirect_uri"] = redirect,
            ["code_verifier"] = verifier,
        };
    }

    internal async Task PasswordAsync(string? organizationAlias = null)
    {
        await Page.Locator("input[name=username], input[name=password]").First.WaitForAsync().ConfigureAwait(false);
        if (await Page.Locator("input[name=password]").CountAsync().ConfigureAwait(false) == 0)
        {
            await Page.Locator("input[name=username]").FillAsync(_username).ConfigureAwait(false);
            await Page.Locator("button[type=submit]").First.ClickAsync().ConfigureAwait(false);
        }

        if (organizationAlias is not null)
        {
            var chooser = await Page.WaitForFunctionAsync("() => window.kcContext?.pageId === 'select-organization.ftl'").ConfigureAwait(false);
            await chooser.DisposeAsync().ConfigureAwait(false);
            string.Equals(new Uri(Page.Url).AbsolutePath, "/mfa-callback", StringComparison.Ordinal).ShouldBeFalse();
            await Page.GetByText(organizationAlias, new PageGetByTextOptions { Exact = true }).ClickAsync().ConfigureAwait(false);
        }

        try
        {
            await Page.Locator("input[name=password]").WaitForAsync().ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            var state = await Page.EvaluateAsync<string>("() => JSON.stringify({page: window.kcContext?.pageId, message: window.kcContext?.message?.summary, title: document.title})").ConfigureAwait(false);
            throw new InvalidOperationException("Native login unavailable: " + state + "; script errors: " + string.Join("; ", _pageErrors), exception);
        }

        if (await Page.Locator("input[name=username]").CountAsync().ConfigureAwait(false) > 0)
        {
            await Page.Locator("input[name=username]").FillAsync(_username).ConfigureAwait(false);
        }

        await Page.Locator("input[name=password]").FillAsync(_password).ConfigureAwait(false);
        await Page.Locator("button[type=submit]").First.ClickAsync().ConfigureAwait(false);
    }

    internal async Task<JsonDocument> ClaimsAsync(Dictionary<string, string> fields)
    {
        await Page.WaitForURLAsync("**/mfa-callback?**").ConfigureAwait(false);
        fields["code"] = QueryHelpers.ParseQuery(new Uri(Page.Url).Query)["code"].ToString();
        using var content = new FormUrlEncodedContent(fields);
        using var response = await _http.PostAsync(new Uri(_server, "realms/teck-local/protocol/openid-connect/token"), content, TestContext.Current.CancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        using var tokens = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(false));
        var token = tokens.RootElement.GetProperty("access_token").GetString()!;

        // Browser assertions inspect the actual issuer response; downstream signature validation is covered by the assembled gateway/service suite.
        return JsonDocument.Parse(WebEncoders.Base64UrlDecode(token.Split('.')[1]));
    }

    internal async Task SubmitFactorAsync(string field, string value)
    {
        await Page.Locator("input[name=" + field + "]").FillAsync(value).ConfigureAwait(false);
        await Page.Locator("#kc-login").ClickAsync().ConfigureAwait(false);
    }

    internal async Task AssertNativeErrorAsync(string pageId)
    {
        var handle = await Page.WaitForFunctionAsync("expected => window.kcContext?.pageId === expected && window.kcContext?.message?.type === 'error'", pageId).ConfigureAwait(false);
        await handle.DisposeAsync().ConfigureAwait(false);
        string.Equals(new Uri(Page.Url).AbsolutePath, "/mfa-callback", StringComparison.Ordinal).ShouldBeFalse();
    }

    internal void AssertNoScriptErrors() => _pageErrors.ShouldBeEmpty();
}
