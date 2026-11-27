namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>
/// Single place where the disposable fixture maps to the production host binders. Every key below
/// is one the target host actually reads; unknown arguments are never invented here.
/// </summary>
internal static class CommerceTestConfiguration
{
    internal const string RealmName = "teck-local";
    internal const string TenantReaderClientId = "tenant-reader";
    internal const string IsolatedDevelopment = "IsolatedDevelopment";

    /// <summary>HmacOrderCursorCodec refuses to construct without at least 32 characters.</summary>
    internal const string CursorSigningKey = "commerce-fixture-cursor-signing-key-0001";

    internal const string SharedPlacementReference = "commerce-placement-shared";
    internal const string DedicatedPlacementReference = "commerce-placement-dedicated";
    internal const string DedicatedReplicaReference = "commerce-placement-dedicated-replica";
    internal const string SharedOfferId = "postgres-shared-monthly";
    internal const string DedicatedOfferId = "postgres-dedicated-monthly";
    internal const string ReplicaAddOnCode = "replicaReadAccess";

    internal static readonly Guid SharedPlacementId = new(0x11111111, 0x1111, 0x4111, 0x81, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11);
    internal static readonly Guid DedicatedPlacementId = new(0x22222222, 0x2222, 0x4222, 0x82, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22);

    internal static string[] Orders(
        CommerceKeycloakFixture keycloak,
        CommercePostgresFixture postgres,
        CommerceRabbitMqFixture rabbit,
        StripeProviderFixture stripe,
        Uri grpc,
        Uri http)
    {
        var args = new List<string>();
        AddOrdersDependencies(args, postgres, rabbit);
        AddOrdersListeners(args, grpc, http);
        AddOrdersIdentity(args, keycloak);
        AddOrdersBilling(args, stripe);
        AddOrdersCatalog(args);
        AddOrdersPlacements(args, keycloak, postgres);
        return [.. args];
    }

    internal static string[] Licensing(
        CommerceKeycloakFixture keycloak,
        CommercePostgresFixture postgres,
        CommerceRabbitMqFixture rabbit,
        CommerceRedisFixture redis,
        Uri grpc,
        Uri orders,
        string activeKeyId,
        IReadOnlyList<(string Key, string Value)> keyRing)
    {
        var args = new List<string>
        {
            Argument("ConnectionStrings:LicensingWrite", postgres.Licensing.ConnectionString),
            Argument("ConnectionStrings:LicensingRead", postgres.Licensing.ConnectionString),
            Argument("ConnectionStrings:rabbitmq", rabbit.Uri.ToString()),
            Argument("ConnectionStrings:redis", redis.ConnectionString),
            Argument("CommerceLicensing:Redis:Standalone", "true"),
            Argument("CommerceLicensing:Grpc:ListenUrl", grpc.ToString()),
            Argument("CommerceLicensing:Grpc:CleartextHttp2", "true"),
            Argument("CommerceLicensing:Database:AutoMigrate", "true"),
            Argument("CommerceLicensing:Messaging:AutoCreate", "true"),
            Argument("CommerceLicensing:ActiveKeyId", activeKeyId),
            Argument("CommerceOrders:GrpcAddress", orders.ToString()),
            Argument("Commerce:Identity:ServerUrl", keycloak.ServerUrl.ToString()),
            Argument("Commerce:Identity:Realm", RealmName),
            Argument("Commerce:Identity:PublicIssuer", keycloak.Issuer.ToString()),
            Argument("Commerce:Identity:Transport", IsolatedDevelopment),
            Argument("Commerce:Identity:TokenEndpoint", keycloak.TokenEndpoint.ToString()),
            Argument("Commerce:Identity:ClientId", keycloak.LicensingClientId),
            Argument("Commerce:Identity:ClientSecret", keycloak.InternalClientSecret),
            Argument("Commerce:Identity:TenantReaderSecret", keycloak.TenantReaderSecret),
        };
        foreach (var (key, value) in keyRing)
        {
            args.Add(Argument(key, value));
        }

        return [.. args];
    }

    internal static string[] CustomerGateway(CommerceKeycloakFixture keycloak, CommerceRedisFixture redis, Uri url, Uri orders, Uri licensing) =>
    [
        Argument("urls", url.ToString()),
        Argument("Commerce:Transport", IsolatedDevelopment),
        Argument("Commerce:Orders:Address", orders.ToString()),
        Argument("Commerce:Licensing:Address", licensing.ToString()),
        Argument("Commerce:Identity:ServerUrl", keycloak.ServerUrl.ToString()),
        Argument("Commerce:Identity:Realm", RealmName),
        Argument("Commerce:Identity:PublicIssuer", keycloak.Issuer.ToString()),
        Argument("Commerce:Identity:Transport", IsolatedDevelopment),
        Argument("Commerce:Identity:TokenEndpoint", keycloak.TokenEndpoint.ToString()),
        Argument("Commerce:Identity:ClientId", keycloak.CustomerGatewayClientId),
        Argument("Commerce:Identity:ClientSecret", keycloak.CustomerGatewayClientSecret),
        Argument("Commerce:Directory:RedisConnectionString", redis.ConnectionString),
        Argument("Commerce:Directory:StandaloneRedis", "true"),
        Argument("Commerce:Directory:ReaderClientId", TenantReaderClientId),
        Argument("Commerce:Directory:ReaderSecret", keycloak.TenantReaderSecret),
    ];

    internal static string[] AdminGateway(CommerceKeycloakFixture keycloak, CommerceRedisFixture redis, Uri url, Uri orders) =>
    [
        Argument("urls", url.ToString()),
        Argument("Commerce:Transport", IsolatedDevelopment),
        Argument("Commerce:Orders:Address", orders.ToString()),
        Argument("Commerce:Identity:ServerUrl", keycloak.ServerUrl.ToString()),
        Argument("Commerce:Identity:Realm", RealmName),
        Argument("Commerce:Identity:PublicIssuer", keycloak.Issuer.ToString()),
        Argument("Commerce:Identity:Transport", IsolatedDevelopment),
        Argument("Commerce:Identity:TokenEndpoint", keycloak.TokenEndpoint.ToString()),
        Argument("Commerce:Identity:ClientId", keycloak.AdminGatewayClientId),
        Argument("Commerce:Identity:ClientSecret", keycloak.AdminGatewayClientSecret),
        Argument("Commerce:Directory:RedisConnectionString", redis.ConnectionString),
        Argument("Commerce:Directory:StandaloneRedis", "true"),
        Argument("Commerce:Directory:ReaderClientId", TenantReaderClientId),
        Argument("Commerce:Directory:ReaderSecret", keycloak.TenantReaderSecret),
    ];

    internal static string Argument(string key, string value) => "--" + key + "=" + value;

    private static void AddOrdersDependencies(List<string> args, CommercePostgresFixture postgres, CommerceRabbitMqFixture rabbit)
    {
        args.Add(Argument("ConnectionStrings:orders", postgres.Orders.ConnectionString));
        args.Add(Argument("ConnectionStrings:orders-read", postgres.Orders.ConnectionString));
        args.Add(Argument("ConnectionStrings:rabbitmq", rabbit.Uri.ToString()));
        args.Add(Argument("CommerceOrders:MessagingSchema", "orders_messaging"));
        args.Add(Argument("CommerceOrders:Cursor:SigningKey", CursorSigningKey));
    }

    private static void AddOrdersListeners(List<string> args, Uri grpc, Uri http)
    {
        args.Add(Argument("CommerceOrders:Listen:GrpcUrl", grpc.ToString()));
        args.Add(Argument("CommerceOrders:Listen:HttpUrl", http.ToString()));
    }

    private static void AddOrdersIdentity(List<string> args, CommerceKeycloakFixture keycloak)
    {
        args.Add(Argument("CommerceOrders:Identity:ServerUrl", keycloak.ServerUrl.ToString()));
        args.Add(Argument("CommerceOrders:Identity:Realm", RealmName));
        args.Add(Argument("CommerceOrders:Identity:PublicIssuer", keycloak.Issuer.ToString()));
        args.Add(Argument("CommerceOrders:Identity:Transport", IsolatedDevelopment));
        args.Add(Argument("CommerceOrders:Identity:Audience", "commerce-orders"));
        args.Add(Argument("CommerceOrders:Identity:TenantReaderClientId", TenantReaderClientId));
        args.Add(Argument("CommerceOrders:Identity:TenantReaderClientSecret", keycloak.TenantReaderSecret));
    }

    private static void AddOrdersBilling(List<string> args, StripeProviderFixture stripe)
    {
        args.Add(Argument("CommerceOrders:Stripe:ApiBaseUrl", stripe.BaseUrl.ToString()));
        args.Add(Argument("CommerceOrders:Stripe:SecretKey", stripe.ApiKey));
        args.Add(Argument("CommerceOrders:Stripe:WebhookSecret", stripe.WebhookSecret));
        args.Add(Argument("CommerceOrders:Stripe:AccountId", StripeProviderFixture.AccountId));
        args.Add(Argument("CommerceOrders:Stripe:Mode", "test"));
        args.Add(Argument("CommerceOrders:Stripe:CheckoutOrigin", "https://checkout.stripe.test"));
        args.Add(Argument("CommerceOrders:Stripe:SuccessUrl", "https://storefront.test/orders/{orderId}?status=success"));
        args.Add(Argument("CommerceOrders:Stripe:CancelUrl", "https://storefront.test/orders/{orderId}?status=cancel"));
        args.Add(Argument("CommerceOrders:Stripe:WebhookToleranceSeconds", "300"));
        args.Add(Argument("CommerceOrders:Stripe:PriceIds:" + SharedOfferId, StripeProviderFixture.SharedPriceId));
        args.Add(Argument("CommerceOrders:Stripe:PriceIds:" + DedicatedOfferId, StripeProviderFixture.DedicatedPriceId));
        args.Add(Argument("CommerceOrders:Stripe:PriceIds:" + ReplicaAddOnCode, StripeProviderFixture.ReplicaPriceId));
    }

    private static void AddOrdersCatalog(List<string> args)
    {
        args.Add(Argument("CommerceOrders:Catalog:Currency", "USD"));
        args.Add(Argument("CommerceOrders:Catalog:Offers:0:OfferId", SharedOfferId));
        args.Add(Argument("CommerceOrders:Catalog:Offers:0:Tier", "shared"));
        args.Add(Argument("CommerceOrders:Catalog:Offers:0:DisplayName", "PostgreSQL shared monthly"));
        args.Add(Argument("CommerceOrders:Catalog:Offers:0:AmountMinor", "1000"));
        args.Add(Argument("CommerceOrders:Catalog:Offers:1:OfferId", DedicatedOfferId));
        args.Add(Argument("CommerceOrders:Catalog:Offers:1:Tier", "dedicated"));
        args.Add(Argument("CommerceOrders:Catalog:Offers:1:DisplayName", "PostgreSQL dedicated monthly"));
        args.Add(Argument("CommerceOrders:Catalog:Offers:1:AmountMinor", "3000"));
        args.Add(Argument("CommerceOrders:Catalog:Offers:1:AddOns:0:Code", ReplicaAddOnCode));
        args.Add(Argument("CommerceOrders:Catalog:Offers:1:AddOns:0:DisplayName", "Read replica access"));
        args.Add(Argument("CommerceOrders:Catalog:Offers:1:AddOns:0:AmountMinor", "500"));
    }

    private static void AddOrdersPlacements(List<string> args, CommerceKeycloakFixture keycloak, CommercePostgresFixture postgres)
    {
        args.Add(Argument("CommerceOrders:Placements:0:PlacementId", SharedPlacementId.ToString("D")));
        args.Add(Argument("CommerceOrders:Placements:0:Tier", "shared"));
        args.Add(Argument("CommerceOrders:Placements:0:PrimarySecretReference", SharedPlacementReference));
        args.Add(Argument("CommerceOrders:Placements:1:PlacementId", DedicatedPlacementId.ToString("D")));
        args.Add(Argument("CommerceOrders:Placements:1:Tier", "dedicated"));
        args.Add(Argument("CommerceOrders:Placements:1:PrimarySecretReference", DedicatedPlacementReference));
        args.Add(Argument("CommerceOrders:Placements:1:ReplicaSecretReference", DedicatedReplicaReference));
        args.Add(Argument("CommerceOrders:Placements:1:DedicatedTenantId", keycloak.TenantOneId.ToString("D")));
        args.Add(Argument("ConnectionStrings:" + SharedPlacementReference, postgres.SharedMarkerConnection));
        args.Add(Argument("ConnectionStrings:" + DedicatedPlacementReference, postgres.DedicatedMarkerConnection));
        args.Add(Argument("ConnectionStrings:" + DedicatedReplicaReference, postgres.DedicatedStandbyMarkerConnection));
    }
}
