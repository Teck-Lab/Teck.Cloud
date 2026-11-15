using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Identity.Protocol;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Playwright;
using Npgsql;
using Shouldly;
using Xunit;

namespace Identity.E2ETests;

/// <summary>Real browser, BFF cookie, token exchange, gRPC authorization and tenant database proof.</summary>
public sealed class ProtectedPathTests
{
    /// <summary>Two tenants share a key without sharing data, and denied callers cannot cross the boundary.</summary>
    /// <returns>The assembled verification lifetime.</returns>
    /// <exception cref="InvalidOperationException">The shared container address is missing.</exception>
    [Fact]
    public Task PkceGatewayGrpcAndPostgresPreserveTenantAndPermissionBoundaries() => RunAsync(includeAdministrator: true);

    /// <summary>Ordinary browser sessions refresh, sign out and respect tenant suspension.</summary>
    /// <returns>The independent assembled verification lifetime.</returns>
    [Fact]
    public Task OrdinarySessionsPreserveTenantIsolationRefreshLogoutAndSuspension() => RunAsync(includeAdministrator: false);

    private static async Task RunAsync(bool includeAdministrator)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(8));
        var advertised = Environment.GetEnvironmentVariable("TECK_REDIS_ADVERTISED_HOST")
            ?? throw new InvalidOperationException("TECK_REDIS_ADVERTISED_HOST must be reachable by the host and containers.");
        var secret = RandomNumberGenerator.GetHexString(48);
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Identity_AppHost>(
            ["--Parameters:redis-advertised-host=" + advertised, "--Parameters:redis-password=" + RandomNumberGenerator.GetHexString(48), "--Parameters:identity-fixture-secret=" + secret], deadline.Token).ConfigureAwait(true);
        var app = await builder.BuildAsync(deadline.Token).ConfigureAwait(true);
        await using var disposeApp = app.ConfigureAwait(true);
        await app.StartAsync(deadline.Token).ConfigureAwait(true);
        _ = await Task.WhenAll(
            app.ResourceNotifications.WaitForResourceHealthyAsync("keycloak", deadline.Token),
            app.ResourceNotifications.WaitForResourceHealthyAsync("identity-gateway", deadline.Token),
            app.ResourceNotifications.WaitForResourceHealthyAsync("identity-service", deadline.Token)).ConfigureAwait(true);
        using var identity = app.CreateHttpClient("keycloak", "http");
        using var gateway = app.CreateHttpClient("identity-gateway", "http");
        using var adminGateway = app.CreateHttpClient("identity-admin-gateway", "http");
        var keycloak = builder.Resources.OfType<KeycloakResource>().Single();
        using var provisioner = new IdentityProvisioner(identity.BaseAddress!);
        await provisioner.InitializeAsync(
            keycloak.AdminUserNameParameter is null ? "admin" : (await keycloak.AdminUserNameParameter.GetValueAsync(deadline.Token).ConfigureAwait(true))!,
            (await keycloak.AdminPasswordParameter.GetValueAsync(deadline.Token).ConfigureAwait(true))!,
            secret,
            gateway.BaseAddress!,
            adminGateway.BaseAddress!,
            deadline.Token).ConfigureAwait(true);
        using var playwright = await Playwright.CreateAsync().ConfigureAwait(true);
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            ExecutablePath = Environment.GetEnvironmentVariable("TECK_PLAYWRIGHT_EXECUTABLE"),
        }).ConfigureAwait(true);
        await using var disposeBrowser = browser.ConfigureAwait(true);
        var alpha = await VerifyOrdinaryAsync(browser, gateway.BaseAddress!, provisioner).ConfigureAwait(true);
        await VerifyRowsAsync((await app.GetConnectionStringAsync("identity-db", deadline.Token).ConfigureAwait(true))!, provisioner, deadline.Token).ConfigureAwait(true);
        if (!includeAdministrator)
        {
            await VerifySuspensionAsync(alpha, gateway.BaseAddress!, provisioner, deadline.Token).ConfigureAwait(true);
            return;
        }

        var totpSecret = await EnrollAdminAsync(browser, identity.BaseAddress!, gateway.BaseAddress!, provisioner.UserPassword).ConfigureAwait(true);
        var milliseconds = 30000 - (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 30000) + 1000;
        await Task.Delay(TimeSpan.FromMilliseconds(milliseconds), deadline.Token).ConfigureAwait(true);
        var administrator = await LoginAsync(browser, adminGateway.BaseAddress!, "platform", "admin-user", provisioner.UserPassword, totpSecret).ConfigureAwait(true);
        var crossTenant = await administrator.Context.APIRequest.GetAsync(new Uri(adminGateway.BaseAddress!, "/api/admin/" + provisioner.BetaId + "/shared-key").ToString()).ConfigureAwait(true);
        var assurance = await administrator.Context.APIRequest.GetAsync(new Uri(adminGateway.BaseAddress!, "/proof/assurance").ToString()).ConfigureAwait(true);
        var exchanged = await administrator.Context.APIRequest.GetAsync(new Uri(adminGateway.BaseAddress!, "/proof/exchanged-assurance").ToString()).ConfigureAwait(true);
        crossTenant.Status.ShouldBe(200, await assurance.TextAsync().ConfigureAwait(true) + " exchanged: " + await exchanged.TextAsync().ConfigureAwait(true));
        (await crossTenant.TextAsync().ConfigureAwait(true)).ShouldBe("beta-value");
        await VerifySuspensionAsync(alpha, gateway.BaseAddress!, provisioner, deadline.Token).ConfigureAwait(true);
        var disabledAdminTarget = await administrator.Context.APIRequest.GetAsync(new Uri(adminGateway.BaseAddress!, "/api/admin/" + provisioner.AlphaId + "/shared-key").ToString()).ConfigureAwait(true);
        disabledAdminTarget.Status.ShouldBe(403);
        await VerifyRowsAsync((await app.GetConnectionStringAsync("identity-db", deadline.Token).ConfigureAwait(true))!, provisioner, deadline.Token).ConfigureAwait(true);
        await VerifyStaleAdministratorAsync(administrator, adminGateway.BaseAddress!, provisioner.BetaId, deadline.Token).ConfigureAwait(true);
        await VerifyLogoutAsync(administrator, adminGateway.BaseAddress!, "platform").ConfigureAwait(true);
    }

    private static async Task VerifyUntrustedClientAsync(IBrowser browser, Uri gateway, IdentityProvisioner provisioner)
    {
        var user = await LoginAsync(browser, gateway, "alpha", "privileged-customer", provisioner.UserPassword).ConfigureAwait(true);
        var read = await user.Context.APIRequest.GetAsync(new Uri(gateway, "/api/records/shared-key").ToString()).ConfigureAwait(true);
        read.Status.ShouldBe(200);
        (await read.TextAsync().ConfigureAwait(true)).ShouldBe("alpha-value");
        var evidence = await user.Context.APIRequest.GetAsync(new Uri(gateway, "/proof/exchanged-assurance").ToString()).ConfigureAwait(true);
        using var claims = JsonDocument.Parse(await evidence.TextAsync().ConfigureAwait(true));
        var values = claims.RootElement.EnumerateArray().ToDictionary(row => row.GetProperty("type").GetString()!, row => row.GetProperty("value").GetString()!, StringComparer.Ordinal);
        values["azp"].ShouldBe("identity-gateway");
        using var access = JsonDocument.Parse(values["realm_access"]);
        access.RootElement.GetProperty("roles").EnumerateArray().Any(role => string.Equals(role.GetString(), "platform-admin", StringComparison.Ordinal)).ShouldBeTrue();
        var denied = await user.Context.APIRequest.GetAsync(new Uri(gateway, "/proof/untrusted-administration/" + provisioner.BetaId + "/shared-key").ToString()).ConfigureAwait(true);
        denied.Status.ShouldBe(403);
    }

    private static async Task VerifyStaleAdministratorAsync(IPage page, Uri gateway, string tenant, CancellationToken cancellationToken)
    {
        var before = await page.Context.APIRequest.GetAsync(new Uri(gateway, "/proof/assurance").ToString()).ConfigureAwait(true);
        using var original = JsonDocument.Parse(await before.TextAsync().ConfigureAwait(true));
        await Task.Delay(TimeSpan.FromSeconds(11), cancellationToken).ConfigureAwait(true);
        await RefreshAsync(page, gateway).ConfigureAwait(true);
        var after = await page.Context.APIRequest.GetAsync(new Uri(gateway, "/proof/assurance").ToString()).ConfigureAwait(true);
        using var renewed = JsonDocument.Parse(await after.TextAsync().ConfigureAwait(true));
        renewed.RootElement.GetProperty("authenticationTimes")[0].GetString().ShouldBe(original.RootElement.GetProperty("authenticationTimes")[0].GetString());
        var denied = await page.Context.APIRequest.GetAsync(new Uri(gateway, "/api/admin/" + tenant + "/shared-key").ToString()).ConfigureAwait(true);
        denied.Status.ShouldBe(403);
    }

    private static async Task RefreshAsync(IPage page, Uri gateway)
    {
        var csrf = await page.Locator("#record input[name=__RequestVerificationToken]").InputValueAsync().ConfigureAwait(true);
        var refreshed = await page.Context.APIRequest.PostAsync(new Uri(gateway, "/session/refresh").ToString(), new APIRequestContextOptions
        {
            Form = page.Context.APIRequest.CreateFormData().Set("__RequestVerificationToken", csrf),
        }).ConfigureAwait(true);
        refreshed.Status.ShouldBe(200);
        (await refreshed.TextAsync().ConfigureAwait(true)).ShouldBe("true");
    }

    private static async Task VerifyRefreshAsync(IPage page, Uri gateway)
    {
        await RefreshAsync(page, gateway).ConfigureAwait(true);
        var read = await page.Context.APIRequest.GetAsync(new Uri(gateway, "/api/records/shared-key").ToString()).ConfigureAwait(true);
        read.Status.ShouldBe(200);
        (await read.TextAsync().ConfigureAwait(true)).ShouldBe("alpha-value");
    }

    private static async Task VerifyLogoutAsync(IPage page, Uri gateway, string organization)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign out", Exact = true }).ClickAsync().ConfigureAwait(true);
        await page.WaitForURLAsync(new Uri(gateway, "/signed-out").ToString()).ConfigureAwait(true);
        var cookies = await page.Context.CookiesAsync().ConfigureAwait(true);
        cookies.ShouldNotContain(cookie => cookie.Name.StartsWith(".AspNetCore.Cookies", StringComparison.Ordinal));
        _ = await page.GotoAsync(new Uri(gateway, "/login/" + organization).ToString()).ConfigureAwait(true);
        await Assertions.Expect(page.Locator("input[name=username]")).ToBeVisibleAsync().ConfigureAwait(true);
    }

    private static async Task<IPage> VerifyOrdinaryAsync(IBrowser browser, Uri gateway, IdentityProvisioner provisioner)
    {
        var alpha = await LoginAsync(browser, gateway, "alpha", "alpha-user", provisioner.UserPassword).ConfigureAwait(true);
        var beta = await LoginAsync(browser, gateway, "beta", "beta-user", provisioner.UserPassword).ConfigureAwait(true);
        foreach (var token in new[] { string.Empty, "invalid" })
        {
            var rejected = await alpha.Context.APIRequest.PostAsync(new Uri(gateway, "/api/records/shared-key").ToString(), new APIRequestContextOptions
            {
                Form = alpha.Context.APIRequest.CreateFormData().Set("value", "csrf-must-not-save").Set("__RequestVerificationToken", token),
            }).ConfigureAwait(true);
            rejected.Status.ShouldBe(400);
        }

        await SaveAsync(alpha, "alpha-value").ConfigureAwait(true);
        await SaveAsync(beta, "beta-value").ConfigureAwait(true);
        await VerifyRefreshAsync(alpha, gateway).ConfigureAwait(true);
        await alpha.Locator("#load").ClickAsync().ConfigureAwait(true);
        await Assertions.Expect(alpha.Locator("#result")).ToHaveTextAsync("alpha-value").ConfigureAwait(true);
        await beta.Locator("#load").ClickAsync().ConfigureAwait(true);
        await Assertions.Expect(beta.Locator("#result")).ToHaveTextAsync("beta-value").ConfigureAwait(true);
        var forged = await alpha.Context.APIRequest.GetAsync(new Uri(gateway, "/api/records/shared-key").ToString(), new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["x-tenant-id"] = provisioner.BetaId },
        }).ConfigureAwait(true);
        forged.Status.ShouldBe(200);
        (await forged.TextAsync().ConfigureAwait(true)).ShouldBe("alpha-value");
        var admin = await alpha.Context.APIRequest.GetAsync(new Uri(gateway, "/api/admin/" + provisioner.BetaId + "/shared-key").ToString()).ConfigureAwait(true);
        admin.Status.ShouldBe(404);
        var denied = await LoginAsync(browser, gateway, "alpha", "denied-user", provisioner.UserPassword).ConfigureAwait(true);
        await denied.Locator("#value").FillAsync("unauthorized-change").ConfigureAwait(true);
        await denied.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync().ConfigureAwait(true);
        await Assertions.Expect(denied.Locator("#result")).ToHaveTextAsync("HTTP 403").ConfigureAwait(true);
        await VerifyUntrustedClientAsync(browser, gateway, provisioner).ConfigureAwait(true);
        await VerifyLogoutAsync(beta, gateway, "beta").ConfigureAwait(true);
        return alpha;
    }

    private static async Task VerifySuspensionAsync(IPage page, Uri gateway, IdentityProvisioner provisioner, CancellationToken cancellationToken)
    {
        await provisioner.DisableAlphaAsync(cancellationToken).ConfigureAwait(true);
        await Task.Delay(TimeSpan.FromSeconds(6), cancellationToken).ConfigureAwait(true);
        var denied = await page.Context.APIRequest.GetAsync(new Uri(gateway, "/api/records/shared-key").ToString()).ConfigureAwait(true);
        denied.Status.ShouldBe(403);
    }

    private static async Task<IPage> LoginAsync(IBrowser browser, Uri gateway, string organization, string username, string password, string? totpSecret = null)
    {
        var page = await StartLoginAsync(browser, gateway, organization, username, password).ConfigureAwait(true);
        if (totpSecret is not null)
        {
            await page.Locator("input[name=otp]").FillAsync(KeycloakTotp.Create(totpSecret)).ConfigureAwait(true);
            await page.Locator("#kc-login").ClickAsync().ConfigureAwait(true);
        }

        try
        {
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Tenant records" })).ToBeVisibleAsync(new() { Timeout = 30000 }).ConfigureAwait(true);
        }
        catch (PlaywrightException error)
        {
            throw new InvalidOperationException("Login stopped at " + new Uri(page.Url).AbsolutePath + ": " + string.Join("; ", await page.Locator("h1,h2,[role=alert],.alert-error").AllTextContentsAsync().ConfigureAwait(true)), error);
        }

        var proof = await page.Context.APIRequest.GetAsync(new Uri(gateway, "/proof/pkce").ToString()).ConfigureAwait(true);
        (await proof.TextAsync().ConfigureAwait(true)).ShouldBe("true");
        var cookies = await page.Context.CookiesAsync().ConfigureAwait(true);
        cookies.Where(cookie => cookie.Name.StartsWith(".AspNetCore.Cookies", StringComparison.Ordinal)).ShouldNotBeEmpty();
        cookies.Where(cookie => cookie.Name.StartsWith(".AspNetCore.Cookies", StringComparison.Ordinal)).ShouldAllBe(cookie => cookie.HttpOnly);
        return page;
    }

    private static async Task<string> EnrollAdminAsync(IBrowser browser, Uri identity, Uri gateway, string password)
    {
        var verifier = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(verifier)));
        var context = await browser.NewContextAsync(new BrowserNewContextOptions { IgnoreHTTPSErrors = true }).ConfigureAwait(true);
        var page = await context.NewPageAsync().ConfigureAwait(true);
        var redirect = new Uri(gateway, "/enrolled");
        var authorize = new Uri(identity, "realms/teck-local/protocol/openid-connect/auth")
            + "?client_id=identity-enrollment&response_type=code&scope=openid&redirect_uri=" + Uri.EscapeDataString(redirect.ToString())
            + "&code_challenge_method=S256&code_challenge=" + challenge;
        _ = await page.GotoAsync(authorize).ConfigureAwait(true);
        await page.Locator("input[name=username]").FillAsync("admin-user").ConfigureAwait(true);
        if (await page.Locator("input[name=password]").CountAsync().ConfigureAwait(true) == 0)
        {
            await page.Locator("input[type=submit],button[type=submit]").First.ClickAsync().ConfigureAwait(true);
        }

        await page.Locator("input[name=password]").FillAsync(password).ConfigureAwait(true);
        await page.Locator("input[type=submit],button[type=submit]").First.ClickAsync().ConfigureAwait(true);
        await page.Locator("input[name=totp]").WaitForAsync().ConfigureAwait(true);
        var secret = await page.Locator("input[name=totpSecret]").InputValueAsync().ConfigureAwait(true);
        await page.Locator("input[name=totp]").FillAsync(KeycloakTotp.Create(secret)).ConfigureAwait(true);
        await page.Locator("input[name=userLabel]").FillAsync("isolated-acceptance").ConfigureAwait(true);
        await page.Locator("#saveTOTPBtn").ClickAsync().ConfigureAwait(true);
        await page.WaitForURLAsync(url => string.Equals(new Uri(url).AbsolutePath, "/enrolled", StringComparison.Ordinal)).ConfigureAwait(true);
        return secret;
    }

    private static async Task<IPage> StartLoginAsync(IBrowser browser, Uri gateway, string organization, string username, string password)
    {
        // Only this disposable browser accepts Aspire's ephemeral development certificate.
        // Production clients retain normal certificate validation; no server/client handler is weakened.
        var context = await browser.NewContextAsync(new BrowserNewContextOptions { IgnoreHTTPSErrors = true }).ConfigureAwait(true);
        var page = await context.NewPageAsync().ConfigureAwait(true);
        var login = new Uri(gateway, "/login/" + organization).ToString();
        try
        {
            _ = await page.GotoAsync(login).ConfigureAwait(true);
        }
        catch (PlaywrightException error) when (error.Message.Contains("net::ERR_NETWORK_CHANGED", StringComparison.Ordinal))
        {
            // Container network creation can change Chromium's network generation before login begins.
            _ = await page.GotoAsync(login).ConfigureAwait(true);
        }

        try
        {
            await page.Locator("input[name=username]").FillAsync(username).ConfigureAwait(true);
        }
        catch (TimeoutException error)
        {
            throw new InvalidOperationException("Login entry stopped at " + new Uri(page.Url).AbsolutePath + ": " + string.Join("; ", await page.Locator("h1,h2,[role=alert],.alert-error,#kc-error-message").AllTextContentsAsync().ConfigureAwait(true)), error);
        }

        if (await page.Locator("input[name=password]").CountAsync().ConfigureAwait(true) == 0)
        {
            await page.Locator("input[type=submit],button[type=submit]").First.ClickAsync().ConfigureAwait(true);
        }

        await page.Locator("input[name=password]").FillAsync(password).ConfigureAwait(true);
        await page.Locator("input[type=submit],button[type=submit]").First.ClickAsync().ConfigureAwait(true);
        return page;
    }

    private static async Task SaveAsync(IPage page, string value)
    {
        await page.Locator("#value").FillAsync(value).ConfigureAwait(true);
        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync().ConfigureAwait(true);
        await Assertions.Expect(page.Locator("#result")).ToHaveTextAsync(value).ConfigureAwait(true);
    }

    private static async Task VerifyRowsAsync(string configuration, IdentityProvisioner provisioner, CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(configuration);
        await using var disposeConnection = connection.ConfigureAwait(true);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(true);
        var command = connection.CreateCommand();
        await using var disposeCommand = command.ConfigureAwait(true);
        command.CommandText = "SELECT \"TenantId\", \"Value\" FROM identity_records WHERE \"Key\" = 'shared-key' ORDER BY \"Value\"";
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(true);
        await using var disposeReader = reader.ConfigureAwait(true);
        (await reader.ReadAsync(cancellationToken).ConfigureAwait(true)).ShouldBeTrue();
        reader.GetString(0).ShouldBe(provisioner.AlphaId);
        reader.GetString(1).ShouldBe("alpha-value");
        (await reader.ReadAsync(cancellationToken).ConfigureAwait(true)).ShouldBeTrue();
        reader.GetString(0).ShouldBe(provisioner.BetaId);
        reader.GetString(1).ShouldBe("beta-value");
        (await reader.ReadAsync(cancellationToken).ConfigureAwait(true)).ShouldBeFalse();
    }
}
