using Microsoft.Playwright;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Checks the account theme against a disposable localized Keycloak realm.</summary>
/// <param name="fixture">Owned PostgreSQL and Keycloak test containers.</param>
public sealed class KeycloakAccountThemeTests(KeycloakFixture fixture) : IClassFixture<KeycloakFixture>
{
    /// <summary>Appearance labels and locale navigation follow the selected realm language.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task AccountAppearanceAndLanguageFollowRealmLocale()
    {
        await using var browser = await KeycloakBrowser.CreateAsync(fixture).ConfigureAwait(true);
        var authorization = await browser.AuthorizeAsync("teck-web").ConfigureAwait(true);
        await browser.PasswordAsync().ConfigureAwait(true);
        using var claims = await browser.ClaimsAsync(authorization).ConfigureAwait(true);

        _ = await browser.Page.GotoAsync(browser.AccountAddress.AbsoluteUri).ConfigureAwait(true);
        browser.Page.Url.ShouldContain("/realms/teck-local/account/");
        await browser.Page.GetByRole(AriaRole.Button, new() { Name = "Change appearance" }).ClickAsync().ConfigureAwait(true);
        (await browser.Page.GetByRole(AriaRole.Menuitem, new() { Name = "Light" }).IsVisibleAsync().ConfigureAwait(true)).ShouldBeTrue();
        await browser.Page.Keyboard.PressAsync("Escape").ConfigureAwait(true);

        await browser.Page.GetByRole(AriaRole.Button, new() { Name = "Language" }).ClickAsync().ConfigureAwait(true);
        await browser.Page.GetByRole(AriaRole.Menuitem, new() { Name = "Dansk" }).ClickAsync().ConfigureAwait(true);
        await browser.Page.GetByRole(AriaRole.Button, new() { Name = "Skift udseende" }).WaitForAsync().ConfigureAwait(true);
        await browser.Page.GetByRole(AriaRole.Button, new() { Name = "Skift udseende" }).ClickAsync().ConfigureAwait(true);
        (await browser.Page.GetByRole(AriaRole.Menuitem, new() { Name = "M\u00f8rk" }).IsVisibleAsync().ConfigureAwait(true)).ShouldBeTrue();
        browser.AssertNoScriptErrors();
    }
}
