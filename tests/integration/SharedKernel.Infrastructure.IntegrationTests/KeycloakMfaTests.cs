using System.Text.Json.Nodes;
using Identity.Protocol;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Exercises the shared themed realm with real browser credentials and native MFA protocols.</summary>
/// <param name="fixture">Owned PostgreSQL and Keycloak test containers.</param>
public sealed class KeycloakMfaTests(KeycloakFixture fixture) : IClassFixture<KeycloakFixture>
{
    /// <summary>Customer enrollment is optional and customer SSO cannot bypass admin MFA.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task PasswordOnlyCustomerCannotEnterAdmin()
    {
        await using var browser = await KeycloakBrowser.CreateAsync(fixture).ConfigureAwait(true);
        var customer = await browser.AuthorizeAsync("teck-web").ConfigureAwait(true);
        await browser.PasswordAsync().ConfigureAwait(true);
        using var claims = await browser.ClaimsAsync(customer).ConfigureAwait(true);
        claims.RootElement.GetProperty("acr").GetString().ShouldBe("1");
        _ = await browser.AuthorizeAsync().ConfigureAwait(true);
        await browser.PasswordAsync().ConfigureAwait(true);
        await browser.AssertNativeErrorAsync("error.ftl").ConfigureAwait(true);
        browser.AssertNoScriptErrors();
    }

    /// <summary>Invalid and reused OTPs fail, while a fresh OTP supplies native admin assurance.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task TotpRejectsInvalidAndReusedCodes()
    {
        await using var browser = await KeycloakBrowser.CreateAsync(fixture, "CONFIGURE_TOTP").ConfigureAwait(true);
        var enrollment = await browser.AuthorizeAsync("teck-web").ConfigureAwait(true);
        await browser.PasswordAsync().ConfigureAwait(true);
        await browser.Page.Locator("input[name=totp]").WaitForAsync().ConfigureAwait(true);
        var secret = await browser.Page.Locator("input[name=totpSecret]").InputValueAsync().ConfigureAwait(true);
        await browser.Page.Locator("input[name=totp]").FillAsync(KeycloakTotp.Create(secret)).ConfigureAwait(true);
        await browser.Page.Locator("input[name=userLabel]").FillAsync("Fixture authenticator").ConfigureAwait(true);
        await browser.Page.Locator("#saveTOTPBtn").ClickAsync().ConfigureAwait(true);
        using var enrolled = await browser.ClaimsAsync(enrollment).ConfigureAwait(true);
        await browser.Context.ClearCookiesAsync().ConfigureAwait(true);
        var login = await browser.AuthorizeAsync().ConfigureAwait(true);
        await browser.PasswordAsync().ConfigureAwait(true);
        await browser.SubmitFactorAsync("otp", "invalid").ConfigureAwait(true);
        await browser.Page.Locator("#input-error-otp-code").WaitForAsync().ConfigureAwait(true);

        // Enrollment consumes its OTP; wait for the next native 30-second counter.
        await Task.Delay(TimeSpan.FromMilliseconds(31000 - (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 30000)), TestContext.Current.CancellationToken).ConfigureAwait(true);
        var code = KeycloakTotp.Create(secret);
        await browser.SubmitFactorAsync("otp", code).ConfigureAwait(true);
        using var accepted = await browser.ClaimsAsync(login).ConfigureAwait(true);
        accepted.RootElement.GetProperty("acr").GetString().ShouldBe("2");
        await browser.Context.ClearCookiesAsync().ConfigureAwait(true);
        _ = await browser.AuthorizeAsync().ConfigureAwait(true);
        await browser.PasswordAsync().ConfigureAwait(true);
        await browser.SubmitFactorAsync("otp", code).ConfigureAwait(true);
        await browser.Page.Locator("#input-error-otp-code").WaitForAsync().ConfigureAwait(true);
        browser.AssertNoScriptErrors();
    }

    /// <summary>Native WebAuthn registration and required user verification complete a strong admin login.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task WebAuthnEnrollmentAndFreshSessionStepUpWork()
    {
        await using var browser = await KeycloakBrowser.CreateAsync(fixture, "webauthn-register").ConfigureAwait(true);
        _ = await browser.Page.AddInitScriptAsync("window.prompt = () => 'Fixture security key'").ConfigureAwait(true);
        var cdp = await browser.Context.NewCDPSessionAsync(browser.Page).ConfigureAwait(true);
        _ = await cdp.SendAsync("WebAuthn.enable", new Dictionary<string, object>(StringComparer.Ordinal) { ["enableUI"] = false }).ConfigureAwait(true);
        var authenticator = await cdp.SendAsync("WebAuthn.addVirtualAuthenticator", new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["options"] = new { protocol = "ctap2", transport = "internal", hasResidentKey = true, hasUserVerification = true, isUserVerified = true, automaticPresenceSimulation = true },
        }).ConfigureAwait(true);
        var enrollment = await browser.AuthorizeAsync("teck-web").ConfigureAwait(true);
        await browser.PasswordAsync().ConfigureAwait(true);
        await browser.Page.Locator("#authenticateWebAuthnButton").ClickAsync().ConfigureAwait(true);
        using var enrolled = await browser.ClaimsAsync(enrollment).ConfigureAwait(true);
        await browser.Context.ClearCookiesAsync().ConfigureAwait(true);
        var login = await browser.AuthorizeAsync().ConfigureAwait(true);
        await browser.PasswordAsync().ConfigureAwait(true);
        await browser.Page.Locator("#authenticateWebAuthnButton").ClickAsync().ConfigureAwait(true);
        using var accepted = await browser.ClaimsAsync(login).ConfigureAwait(true);
        accepted.RootElement.GetProperty("acr").GetString().ShouldBe("2");
        var authenticatedAt = accepted.RootElement.GetProperty("auth_time").GetInt64();
        var reuse = await browser.AuthorizeAsync().ConfigureAwait(true);
        using var reused = await browser.ClaimsAsync(reuse).ConfigureAwait(true);
        reused.RootElement.GetProperty("acr").GetString().ShouldBe("2");
        reused.RootElement.GetProperty("auth_time").GetInt64().ShouldBe(authenticatedAt);
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken).ConfigureAwait(true);
        var fresh = await browser.AuthorizeAsync(maximumAge: "0").ConfigureAwait(true);
        await browser.PasswordAsync().ConfigureAwait(true);
        await browser.Page.Locator("#authenticateWebAuthnButton").ClickAsync().ConfigureAwait(true);
        using var refreshed = await browser.ClaimsAsync(fresh).ConfigureAwait(true);
        refreshed.RootElement.GetProperty("acr").GetString().ShouldBe("2");
        refreshed.RootElement.GetProperty("auth_time").GetInt64().ShouldBeGreaterThan(authenticatedAt);
        await browser.Context.ClearCookiesAsync().ConfigureAwait(true);
        _ = await browser.AuthorizeAsync().ConfigureAwait(true);
        await browser.PasswordAsync().ConfigureAwait(true);
        _ = await cdp.SendAsync("WebAuthn.setUserVerified", new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["authenticatorId"] = authenticator!.Value.GetProperty("authenticatorId").GetString()!,
            ["isUserVerified"] = false,
        }).ConfigureAwait(true);
        await browser.Page.Locator("#authenticateWebAuthnButton").ClickAsync().ConfigureAwait(true);
        var rejected = await browser.Page.WaitForFunctionAsync("() => window.kcContext?.pageId === 'webauthn-error.ftl'").ConfigureAwait(true);
        await rejected.DisposeAsync().ConfigureAwait(true);
        await AssertRemovedCredentialFailsAsync(browser).ConfigureAwait(true);
        browser.AssertNoScriptErrors();
    }

    /// <summary>Recovery codes are accepted once and a used code cannot authenticate again.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task RecoveryCodesAreSingleUse()
    {
        await using var browser = await KeycloakBrowser.CreateAsync(fixture, "CONFIGURE_RECOVERY_AUTHN_CODES").ConfigureAwait(true);
        var enrollment = await browser.AuthorizeAsync("teck-web").ConfigureAwait(true);
        await browser.PasswordAsync().ConfigureAwait(true);
        await browser.Page.Locator("#kcRecoveryCodesConfirmationCheck").WaitForAsync().ConfigureAwait(true);
        var codes = await browser.Page.EvaluateAsync<string[]>("() => window.kcContext.recoveryAuthnCodesConfigBean.generatedRecoveryAuthnCodesList").ConfigureAwait(true);
        codes.Length.ShouldBe(12);
        await browser.Page.Locator("#kcRecoveryCodesConfirmationCheck").CheckAsync().ConfigureAwait(true);
        await browser.Page.Locator("#saveRecoveryAuthnCodesBtn").ClickAsync().ConfigureAwait(true);
        using var enrolled = await browser.ClaimsAsync(enrollment).ConfigureAwait(true);
        await browser.Context.ClearCookiesAsync().ConfigureAwait(true);
        var login = await browser.AuthorizeAsync().ConfigureAwait(true);
        await browser.PasswordAsync().ConfigureAwait(true);
        await browser.SubmitFactorAsync("recoveryCodeInput", codes[0]).ConfigureAwait(true);
        using var accepted = await browser.ClaimsAsync(login).ConfigureAwait(true);
        accepted.RootElement.GetProperty("acr").GetString().ShouldBe("2");
        await browser.Context.ClearCookiesAsync().ConfigureAwait(true);
        var replay = await browser.AuthorizeAsync().ConfigureAwait(true);
        await browser.PasswordAsync().ConfigureAwait(true);
        await browser.SubmitFactorAsync("recoveryCodeInput", codes[0]).ConfigureAwait(true);
        await browser.AssertNativeErrorAsync("login-recovery-authn-code-input.ftl").ConfigureAwait(true);
        await browser.SubmitFactorAsync("recoveryCodeInput", codes[1]).ConfigureAwait(true);
        using var nextCode = await browser.ClaimsAsync(replay).ConfigureAwait(true);
        nextCode.RootElement.GetProperty("acr").GetString().ShouldBe("2");
        browser.AssertNoScriptErrors();
    }

    /// <summary>The native organization chooser preserves the selected immutable organization identity.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task OrganizationSelectionUsesNativeThemedPage()
    {
        await using var browser = await KeycloakBrowser.CreateAsync(fixture).ConfigureAwait(true);
        var alias = "browser-" + Guid.NewGuid().ToString("N");
        var selectedId = string.Empty;
        for (var index = 0; index < 2; index++)
        {
            var name = alias + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            using var created = await fixture.AdminAsync(HttpMethod.Post, "organizations", new JsonObject { ["name"] = name, ["alias"] = name, ["enabled"] = true }, TestContext.Current.CancellationToken).ConfigureAwait(true);
            using var organizations = await fixture.AdminAsync(HttpMethod.Get, "organizations?search=" + name, body: null, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var id = organizations.RootElement[0].GetProperty("id").GetString();
            using var added = await fixture.AdminAsync(HttpMethod.Post, "organizations/" + id + "/members", JsonValue.Create(browser.UserId), TestContext.Current.CancellationToken).ConfigureAwait(true);
            if (index == 0)
            {
                selectedId = id;
            }
        }

        var login = await browser.AuthorizeAsync("teck-web", scope: "openid organization").ConfigureAwait(true);
        await browser.PasswordAsync(alias + "0").ConfigureAwait(true);
        using var claims = await browser.ClaimsAsync(login).ConfigureAwait(true);
        var organizationsClaim = claims.RootElement.GetProperty("organization");
        organizationsClaim.EnumerateObject().Count().ShouldBe(1);
        organizationsClaim.GetProperty(alias + "0").GetProperty("id").GetString().ShouldBe(selectedId);
        browser.AssertNoScriptErrors();
    }

    private async Task AssertRemovedCredentialFailsAsync(KeycloakBrowser browser)
    {
        using var credentials = await fixture.AdminAsync(HttpMethod.Get, "users/" + browser.UserId + "/credentials", body: null, TestContext.Current.CancellationToken).ConfigureAwait(true);
        foreach (var credential in credentials.RootElement.EnumerateArray().Where(value => string.Equals(value.GetProperty("type").GetString(), "webauthn", StringComparison.Ordinal)))
        {
            using var removed = await fixture.AdminAsync(HttpMethod.Delete, "users/" + browser.UserId + "/credentials/" + credential.GetProperty("id").GetString(), body: null, TestContext.Current.CancellationToken).ConfigureAwait(true);
        }

        await browser.Context.ClearCookiesAsync().ConfigureAwait(true);
        _ = await browser.AuthorizeAsync().ConfigureAwait(true);
        await browser.PasswordAsync().ConfigureAwait(true);
        await browser.AssertNativeErrorAsync("error.ftl").ConfigureAwait(true);
    }
}
