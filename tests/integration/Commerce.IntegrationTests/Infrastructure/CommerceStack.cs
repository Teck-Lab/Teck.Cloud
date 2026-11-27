using AdminGatewayHostApi = Teck.Cloud.Commerce.AdminGateway.Host.AdminGatewayHost;
using CustomerGatewayHostApi = Teck.Cloud.Commerce.CustomerGateway.Host.CustomerGatewayHost;
using LicensingHostApi = Teck.Cloud.Commerce.Licensing.Host.LicensingHost;
using OrdersHostApi = Teck.Cloud.Commerce.Orders.Host.OrdersHost;

namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>The real commerce topology: PostgreSQL, RabbitMQ, Keycloak, Stripe shape and four hosts.</summary>
internal sealed class CommerceStack : IAsyncDisposable
{
    private CommerceHost? _orders;
    private CommerceHost? _licensing;
    private CommerceHost? _customerGatewayHost;
    private CommerceHost? _adminGatewayHost;

    private CommerceStack(CommerceKeycloakFixture keycloak)
    {
        Keycloak = keycloak;
        Postgres = new CommercePostgresFixture();
        Rabbit = new CommerceRabbitMqFixture();
        Redis = new CommerceRedisFixture();
        Stripe = new StripeProviderFixture();
        OrdersAddress = FreePort.Http(FreePort.Next());
        OrdersWebhookAddress = FreePort.Http(FreePort.Next());
        LicensingAddress = FreePort.Http(FreePort.Next());
        CustomerGatewayAddress = FreePort.Http(FreePort.Next());
        AdminGatewayAddress = FreePort.Http(FreePort.Next());
    }

    internal CommerceKeycloakFixture Keycloak { get; }

    internal CommercePostgresFixture Postgres { get; }

    internal CommerceRabbitMqFixture Rabbit { get; }

    internal CommerceRedisFixture Redis { get; }

    internal StripeProviderFixture Stripe { get; }

    internal CommerceKeyRing KeyRing { get; } = new();

    internal Uri OrdersAddress { get; }

    internal Uri OrdersWebhookAddress { get; }

    internal Uri LicensingAddress { get; }

    internal Uri CustomerGatewayAddress { get; }

    internal Uri AdminGatewayAddress { get; }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_adminGatewayHost is not null)
        {
            await _adminGatewayHost.DisposeAsync().ConfigureAwait(false);
        }

        if (_customerGatewayHost is not null)
        {
            await _customerGatewayHost.DisposeAsync().ConfigureAwait(false);
        }

        if (_licensing is not null)
        {
            await _licensing.DisposeAsync().ConfigureAwait(false);
        }

        if (_orders is not null)
        {
            await _orders.DisposeAsync().ConfigureAwait(false);
        }

        await Stripe.DisposeAsync().ConfigureAwait(false);
        await Redis.DisposeAsync().ConfigureAwait(false);
        await Rabbit.DisposeAsync().ConfigureAwait(false);
        await Postgres.DisposeAsync().ConfigureAwait(false);
    }

    internal static async Task<CommerceStack> StartAsync(CommerceKeycloakFixture keycloak, CancellationToken cancellationToken)
    {
        var stack = new CommerceStack(keycloak);
        await stack.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return stack;
    }

    internal Task RestartOrdersAsync(CancellationToken cancellationToken) =>
        _orders is null
            ? throw new InvalidOperationException("Orders is not running.")
            : _orders.RestartAsync(cancellationToken);

    internal Task RestartLicensingWithKeyRingAsync(
        string activeKeyId,
        IReadOnlyList<(string Key, string Value)> keyRing,
        CancellationToken cancellationToken) =>
        _licensing is null
            ? throw new InvalidOperationException("Licensing is not running.")
            : _licensing.RestartAsync(LicensingArgs(activeKeyId, keyRing), cancellationToken);

    internal Task RotateLicensingToK2Async(CancellationToken cancellationToken) =>
        RestartLicensingWithKeyRingAsync("K2", KeyRing.RotatedToK2(), cancellationToken);

    internal Task RemoveK1VerificationKeyAsync(CancellationToken cancellationToken) =>
        RestartLicensingWithKeyRingAsync("K2", KeyRing.K2Only(), cancellationToken);

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.WhenAll(
                Postgres.InitializeAsync().AsTask(),
                Rabbit.InitializeAsync().AsTask(),
                Redis.InitializeAsync().AsTask(),
                Stripe.InitializeAsync().AsTask()).ConfigureAwait(false);
            _orders = await CommerceHost.StartAsync(
                OrdersHostApi.Build,
                CommerceTestConfiguration.Orders(Keycloak, Postgres, Rabbit, Stripe, OrdersAddress, OrdersWebhookAddress),
                OrdersWebhookAddress,
                OrdersAddress,
                cancellationToken).ConfigureAwait(false);
            _licensing = await CommerceHost.StartAsync(
                LicensingHostApi.Build,
                LicensingArgs("K1", KeyRing.Initial()),
                httpAddress: null,
                grpcAddress: LicensingAddress,
                cancellationToken).ConfigureAwait(false);
            _customerGatewayHost = await CommerceHost.StartAsync(
                CustomerGatewayHostApi.Build,
                CommerceTestConfiguration.CustomerGateway(Keycloak, Redis, CustomerGatewayAddress, OrdersAddress, LicensingAddress),
                httpAddress: CustomerGatewayAddress,
                grpcAddress: null,
                cancellationToken).ConfigureAwait(false);
            _adminGatewayHost = await CommerceHost.StartAsync(
                AdminGatewayHostApi.Build,
                CommerceTestConfiguration.AdminGateway(Keycloak, Redis, AdminGatewayAddress, OrdersAddress),
                httpAddress: AdminGatewayAddress,
                grpcAddress: null,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private string[] LicensingArgs(string activeKeyId, IReadOnlyList<(string Key, string Value)> keyRing) =>
        CommerceTestConfiguration.Licensing(Keycloak, Postgres, Rabbit, Redis, LicensingAddress, OrdersAddress, activeKeyId, keyRing);
}
