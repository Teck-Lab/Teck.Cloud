using Identity.E2ETests;
using Microsoft.Playwright;

namespace Identity.ClusterProbe;

/// <summary>Disposable in-cluster acceptance client for the actual gateway/service fixture.</summary>
public static class Program
{
    private const string RecordTokenSelector = "#record input[name=__RequestVerificationToken]";

    /// <summary>Verifies real code-flow cookies, exchange, permissions and tenant persistence.</summary>
    /// <param name="args">Use --browser-smoke to validate the non-root browser without network access.</param>
    /// <returns>The bounded probe lifetime.</returns>
    public static async Task Main(string[] args)
    {
        if (args is ["--browser-smoke"])
        {
            using var driver = await Playwright.CreateAsync().ConfigureAwait(false);
            var smokeBrowser = await driver.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true }).ConfigureAwait(false);
            await using var disposeSmokeBrowser = smokeBrowser.ConfigureAwait(false);
            var smokePage = await smokeBrowser.NewPageAsync().ConfigureAwait(false);
            var pageHtml = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Assets", "index.html")).ConfigureAwait(false);
            await smokePage.SetContentAsync(pageHtml.Replace("{{token}}", "smoke-csrf-token", StringComparison.Ordinal)).ConfigureAwait(false);
            Require(string.Equals(await smokePage.TitleAsync().ConfigureAwait(false), "Identity foundation proof", StringComparison.Ordinal), "Headless browser did not render the probe page.");
            Require(string.Equals(await smokePage.Locator("h1").InnerTextAsync().ConfigureAwait(false), "Tenant records", StringComparison.Ordinal), "Headless browser DOM is unavailable.");
            Require(string.Equals(await smokePage.Locator(RecordTokenSelector).InputValueAsync().ConfigureAwait(false), "smoke-csrf-token", StringComparison.Ordinal), "Record form antiforgery token is unavailable.");
            Console.WriteLine("teck-kind-playwright-browser-ready");
            return;
        }

        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var gateway = new Uri(Required("GATEWAY_URL"));
        using var provisioner = new IdentityProvisioner(new Uri(Required("IDENTITY_BACKEND")));
        await provisioner.InitializeAsync(Required("ADMIN_USERNAME"), Required("ADMIN_PASSWORD"), Required("GATEWAY_SECRET"), gateway, gateway, deadline.Token).ConfigureAwait(false);
        var resolved = await System.Net.Dns.GetHostAddressesAsync(gateway.Host, deadline.Token).ConfigureAwait(false);
        Console.WriteLine("Application Gateway resolved to: " + string.Join(", ", resolved.Select(address => address.ToString())));
        using (var socket = new System.Net.Sockets.TcpClient())
        {
            using var connectDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            connectDeadline.CancelAfter(TimeSpan.FromSeconds(10));
            await socket.ConnectAsync(gateway.Host, gateway.Port, connectDeadline.Token).ConfigureAwait(false);
        }

        using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true, Args = ["--host-rules=MAP localhost " + Required("IDENTITY_GATEWAY_ADDRESS")] }).ConfigureAwait(false);
        await using var disposeBrowser = browser.ConfigureAwait(false);
        var alpha = await LoginAsync(browser, gateway, "alpha", "alpha-user", provisioner.UserPassword).ConfigureAwait(false);
        var beta = await LoginAsync(browser, gateway, "beta", "beta-user", provisioner.UserPassword).ConfigureAwait(false);
        await StoreAsync(alpha, gateway, "alpha-value", expectedStatus: 200).ConfigureAwait(false);
        await StoreAsync(beta, gateway, "beta-value", expectedStatus: 200).ConfigureAwait(false);
        await ReadAsync(alpha, gateway, "alpha-value", provisioner.BetaId).ConfigureAwait(false);
        await ReadAsync(beta, gateway, "beta-value", provisioner.AlphaId).ConfigureAwait(false);
        var denied = await LoginAsync(browser, gateway, "alpha", "denied-user", provisioner.UserPassword).ConfigureAwait(false);
        await StoreAsync(denied, gateway, "must-not-persist", expectedStatus: 403).ConfigureAwait(false);
        await ReadAsync(alpha, gateway, "alpha-value", provisioner.BetaId).ConfigureAwait(false);
        await provisioner.DisableAlphaAsync(deadline.Token).ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromSeconds(6), deadline.Token).ConfigureAwait(false);
        var disabled = await alpha.Context.APIRequest.GetAsync(new Uri(gateway, "/api/records/shared-key").ToString()).ConfigureAwait(false);
        Require(disabled.Status == 403, "Disabled organization remained authorized.");
        await ReadAsync(beta, gateway, "beta-value", provisioner.AlphaId).ConfigureAwait(false);
        Console.WriteLine("teck-kind-protected-path-ready");
    }

    private static async Task<IPage> LoginAsync(IBrowser browser, Uri gateway, string organization, string username, string password)
    {
        var context = await browser.NewContextAsync().ConfigureAwait(false);
        var page = await context.NewPageAsync().ConfigureAwait(false);
        _ = await page.GotoAsync(new Uri(gateway, "/login/" + organization).ToString()).ConfigureAwait(false);
        await page.Locator("input[name=username]").FillAsync(username).ConfigureAwait(false);
        if (await page.Locator("input[name=password]").CountAsync().ConfigureAwait(false) == 0)
        {
            await page.Locator("input[type=submit],button[type=submit]").First.ClickAsync().ConfigureAwait(false);
        }

        await page.Locator("input[name=password]").FillAsync(password).ConfigureAwait(false);
        await page.Locator("input[type=submit],button[type=submit]").First.ClickAsync().ConfigureAwait(false);
        await page.GetByRole(AriaRole.Heading, new() { Name = "Tenant records" }).WaitForAsync().ConfigureAwait(false);
        var pkce = await context.APIRequest.GetAsync(new Uri(gateway, "/proof/pkce").ToString()).ConfigureAwait(false);
        Require(pkce.Status == 200 && string.Equals(await pkce.TextAsync().ConfigureAwait(false), "true", StringComparison.Ordinal), "The gateway did not redeem the PKCE code.");
        return page;
    }

    private static async Task StoreAsync(IPage page, Uri gateway, string value, int expectedStatus)
    {
        var token = await page.Locator(RecordTokenSelector).InputValueAsync().ConfigureAwait(false);
        var result = await page.Context.APIRequest.PostAsync(new Uri(gateway, "/api/records/shared-key").ToString(), new APIRequestContextOptions
        {
            Form = page.Context.APIRequest.CreateFormData().Set("value", value).Set("__RequestVerificationToken", token),
        }).ConfigureAwait(false);
        Require(result.Status == expectedStatus, "Unexpected protected write decision.");
    }

    private static async Task ReadAsync(IPage page, Uri gateway, string expected, string forgedTenant)
    {
        var response = await page.Context.APIRequest.GetAsync(new Uri(gateway, "/api/records/shared-key").ToString(), new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["x-tenant-id"] = forgedTenant },
        }).ConfigureAwait(false);
        Require(response.Status == 200 && string.Equals(await response.TextAsync().ConfigureAwait(false), expected, StringComparison.Ordinal), "Tenant-isolated persisted read failed.");
    }

    private static string Required(string key) => Environment.GetEnvironmentVariable(key) ?? throw new InvalidOperationException("A probe setting is missing.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
