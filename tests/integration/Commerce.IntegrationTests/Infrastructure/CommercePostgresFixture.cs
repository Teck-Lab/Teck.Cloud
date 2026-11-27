using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Networks;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>Disposable service databases, shared placement target and a physical streaming standby.</summary>
public sealed class CommercePostgresFixture : IAsyncLifetime
{
    private const string User = "placement";
    private const string ReloadConfigSql = "SELECT pg_reload_conf();";
    private const string PlacementBindingUpsertSql =
        "INSERT INTO teck_commerce.placement_binding (tenant_id, order_id, subscription_id, placement_id)\n" +
        "VALUES ($1, $2, $3, $4)\n" +
        "ON CONFLICT (tenant_id, order_id, subscription_id) DO UPDATE SET placement_id = EXCLUDED.placement_id;";

    private const string StandbyBindingSql =
        "SELECT pg_is_in_recovery(), EXISTS (\n" +
        "    SELECT 1 FROM teck_commerce.placement_binding\n" +
        "    WHERE tenant_id = $1 AND order_id = $2 AND subscription_id = $3 AND placement_id = $4);";

    private const string SetRoleSettingsSql =
        "SELECT set_config('commerce.fixture_new_role', $1, true), " +
        "set_config('commerce.fixture_new_role_password', $2, true);";

    private const string CreateLoginRoleSql =
        "DO $do$ BEGIN EXECUTE format('CREATE ROLE %I WITH LOGIN PASSWORD %L', " +
        "current_setting('commerce.fixture_new_role'), " +
        "current_setting('commerce.fixture_new_role_password')); END $do$;";

    private const string CreateReplicationRoleSql =
        "DO $do$ BEGIN EXECUTE format('CREATE ROLE %I WITH REPLICATION LOGIN PASSWORD %L', " +
        "current_setting('commerce.fixture_new_role'), " +
        "current_setting('commerce.fixture_new_role_password')); END $do$;";

    private const string SharedTargetSetupSql =
        "CREATE SCHEMA teck_commerce;\n" +
        "CREATE TABLE teck_commerce.placement_binding (\n" +
        "    tenant_id uuid NOT NULL,\n" +
        "    order_id uuid NOT NULL,\n" +
        "    subscription_id uuid NOT NULL,\n" +
        "    placement_id text NOT NULL,\n" +
        "    PRIMARY KEY (tenant_id, order_id, subscription_id));\n" +
        "CREATE SCHEMA tenant_one;\n" +
        "CREATE SCHEMA tenant_two;\n" +
        "CREATE TABLE tenant_one.fixture_row (value text NOT NULL);\n" +
        "CREATE TABLE tenant_two.fixture_row (value text NOT NULL);\n" +
        "INSERT INTO tenant_one.fixture_row VALUES ('tenant-one-row');\n" +
        "INSERT INTO tenant_two.fixture_row VALUES ('tenant-two-row');\n" +
        "GRANT USAGE ON SCHEMA tenant_one TO fixture_tenant_one;\n" +
        "GRANT SELECT ON tenant_one.fixture_row TO fixture_tenant_one;\n" +
        "GRANT USAGE ON SCHEMA tenant_two TO fixture_tenant_two;\n" +
        "GRANT SELECT ON tenant_two.fixture_row TO fixture_tenant_two;\n" +
        "GRANT USAGE ON SCHEMA teck_commerce TO commerce_marker_reader;\n" +
        "GRANT SELECT ON teck_commerce.placement_binding TO commerce_marker_reader;";

    private const string DedicatedTargetSetupSql =
        "CREATE SCHEMA teck_commerce;\n" +
        "CREATE TABLE teck_commerce.placement_binding (\n" +
        "    tenant_id uuid NOT NULL,\n" +
        "    order_id uuid NOT NULL,\n" +
        "    subscription_id uuid NOT NULL,\n" +
        "    placement_id text NOT NULL,\n" +
        "    PRIMARY KEY (tenant_id, order_id, subscription_id));\n" +
        "GRANT USAGE ON SCHEMA teck_commerce TO commerce_marker_reader;\n" +
        "GRANT SELECT ON teck_commerce.placement_binding TO commerce_marker_reader;";

    private static readonly string StartScript = string.Join(
        '\n',
        "set -e",
        "install -d -o postgres -g postgres \"$PGDATA\"",
        "cp /tmp/commerce-tls/server.crt /var/lib/postgresql/server.crt",
        "cp /tmp/commerce-tls/server.key /var/lib/postgresql/server.key",
        "chown postgres:postgres /var/lib/postgresql/server.crt /var/lib/postgresql/server.key",
        "chmod 600 /var/lib/postgresql/server.key",
        "exec /usr/local/bin/docker-entrypoint.sh postgres \\",
        "  -c ssl=on \\",
        "  -c ssl_cert_file=/var/lib/postgresql/server.crt \\",
        "  -c ssl_key_file=/var/lib/postgresql/server.key");

    private static readonly string[] EnableReplicationHostRule =
        [
            "bash",
            "-c",
            "echo 'host replication replicator 0.0.0.0/0 scram-sha-256' >> \"$PGDATA/pg_hba.conf\"",
        ];

    private readonly string _password = RandomNumberGenerator.GetHexString(48);
    private readonly string _replicationPassword = RandomNumberGenerator.GetHexString(48);
    private readonly string _tenantOnePassword = RandomNumberGenerator.GetHexString(48);
    private readonly string _tenantTwoPassword = RandomNumberGenerator.GetHexString(48);
    private readonly string _tlsDirectory = Path.Combine(Path.GetTempPath(), "commerce-tls-" + RandomNumberGenerator.GetHexString(12));
    private readonly INetwork _network = new NetworkBuilder().Build();
    private PostgreSqlContainer _orders = null!;
    private PostgreSqlContainer _licensing = null!;
    private PostgreSqlContainer _shared = null!;
    private PostgreSqlContainer _dedicated = null!;
    private PostgreSqlContainer _standby = null!;

    /// <summary>Gets the Orders service database endpoint.</summary>
    public PostgresEndpoint Orders { get; private set; } = null!;

    /// <summary>Gets the Licensing service database endpoint.</summary>
    public PostgresEndpoint Licensing { get; private set; } = null!;

    /// <summary>Gets the shared precreated placement target.</summary>
    public PostgresEndpoint SharedTarget { get; private set; } = null!;

    /// <summary>Gets the dedicated precreated tenant placement target.</summary>
    public PostgresEndpoint DedicatedTarget { get; private set; } = null!;

    /// <summary>Gets the physical read-only standby of the dedicated target.</summary>
    public PostgresEndpoint DedicatedStandby { get; private set; } = null!;

    /// <summary>Gets the shared-target read-only marker connection.</summary>
    public string SharedMarkerConnection => Connection(SharedTarget, "commerce_marker_reader", _password);

    /// <summary>Gets the dedicated primary read-only marker connection.</summary>
    public string DedicatedMarkerConnection => Connection(DedicatedTarget, "commerce_marker_reader", _password);

    /// <summary>Gets the dedicated standby read-only marker connection.</summary>
    public string DedicatedStandbyMarkerConnection => Connection(DedicatedStandby, "commerce_marker_reader", _password);

    /// <summary>Gets the tenant-one fixture role connection string for the shared target.</summary>
    public string TenantOneReadConnection => Connection(SharedTarget, "fixture_tenant_one", _tenantOnePassword);

    /// <summary>Gets the tenant-two fixture role connection string for the shared target.</summary>
    public string TenantTwoReadConnection => Connection(SharedTarget, "fixture_tenant_two", _tenantTwoPassword);

    private string StandbyScript => string.Join(
        '\n',
        "set -e",
        "until pg_isready -h commerce-dedicated-target -p 5432 -U replicator; do sleep 1; done",
        "install -d -o postgres -g postgres \"$PGDATA\"",
        "rm -rf \"$PGDATA\"/*",
        $"PGPASSWORD={_replicationPassword} pg_basebackup \\",
        "  -h commerce-dedicated-target -p 5432 -U replicator \\",
        "  -D \"$PGDATA\" -Fp -Xs -R",
        "chown -R postgres:postgres \"$PGDATA\"",
        "cp /tmp/commerce-tls/server.crt /var/lib/postgresql/server.crt",
        "cp /tmp/commerce-tls/server.key /var/lib/postgresql/server.key",
        "chown postgres:postgres /var/lib/postgresql/server.crt /var/lib/postgresql/server.key",
        "chmod 600 /var/lib/postgresql/server.key",
        "exec /usr/local/bin/docker-entrypoint.sh postgres \\",
        "  -c ssl=on \\",
        "  -c ssl_cert_file=/var/lib/postgresql/server.crt \\",
        "  -c ssl_key_file=/var/lib/postgresql/server.key");

    /// <summary>Inserts the manual-operator placement binding on a precreated target.</summary>
    /// <param name="target">The placement target endpoint.</param>
    /// <param name="binding">Authoritative tenant/order/subscription/placement binding.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The insert task.</returns>
    public static async Task BindPlacementAsync(PostgresEndpoint target, PlacementBinding binding, CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(target.AdminConnectionString);
        await using var connectionLifetime = connection.ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        await using var commandLifetime = command.ConfigureAwait(false);
        command.CommandText = PlacementBindingUpsertSql;
        _ = command.Parameters.AddWithValue(binding.TenantId);
        _ = command.Parameters.AddWithValue(binding.OrderId);
        _ = command.Parameters.AddWithValue(binding.SubscriptionId);
        _ = command.Parameters.AddWithValue(binding.PlacementId.ToString("D"));
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(6));
        CreateTlsFiles();
        await _network.CreateAsync(deadline.Token).ConfigureAwait(false);
        _orders = CreateContainer("commerce-orders-db", "orders");
        _licensing = CreateContainer("commerce-licensing-db", "licensing");
        _shared = CreateContainer("commerce-shared-target", "commerce_fixture");
        _dedicated = CreateContainer("commerce-dedicated-target", "dedicated_tenant");
        await Task.WhenAll(
            _orders.StartAsync(deadline.Token),
            _licensing.StartAsync(deadline.Token),
            _shared.StartAsync(deadline.Token),
            _dedicated.StartAsync(deadline.Token)).ConfigureAwait(false);
        Orders = Endpoint(_orders, "orders");
        Licensing = Endpoint(_licensing, "licensing");
        SharedTarget = Endpoint(_shared, "commerce_fixture");
        DedicatedTarget = Endpoint(_dedicated, "dedicated_tenant");

        var replicationAdmin = new NpgsqlConnection(DedicatedTarget.AdminConnectionString);
        await using (replicationAdmin.ConfigureAwait(false))
        {
            await replicationAdmin.OpenAsync(deadline.Token).ConfigureAwait(false);
            await CreateReplicationRoleAsync(replicationAdmin, "replicator", _replicationPassword, deadline.Token).ConfigureAwait(false);
            var configCommand = replicationAdmin.CreateCommand();
            await using var configCommandLifetime = configCommand.ConfigureAwait(false);
            configCommand.CommandText = ReloadConfigSql;
            _ = await configCommand.ExecuteNonQueryAsync(deadline.Token).ConfigureAwait(false);
        }

        var hba = await _dedicated.ExecAsync(EnableReplicationHostRule, deadline.Token).ConfigureAwait(false);
        if (hba.ExitCode != 0)
        {
            throw new InvalidOperationException("The fixture could not enable the replication host rule: " + hba.Stderr);
        }

        var reloadConnection = new NpgsqlConnection(DedicatedTarget.AdminConnectionString);
        await using (reloadConnection.ConfigureAwait(false))
        {
            await reloadConnection.OpenAsync(deadline.Token).ConfigureAwait(false);
            var reloadCommand = reloadConnection.CreateCommand();
            await using var reloadCommandLifetime = reloadCommand.ConfigureAwait(false);
            reloadCommand.CommandText = ReloadConfigSql;
            _ = await reloadCommand.ExecuteNonQueryAsync(deadline.Token).ConfigureAwait(false);
        }

        await InitializeSharedAsync(deadline.Token).ConfigureAwait(false);
        await InitializeDedicatedAsync(deadline.Token).ConfigureAwait(false);
        _standby = CreateStandby();
        await _standby.StartAsync(deadline.Token).ConfigureAwait(false);
        DedicatedStandby = Endpoint(_standby, "dedicated_tenant");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (var container in new[] { _standby, _dedicated, _shared, _licensing, _orders })
        {
            if (container is not null)
            {
                await container.DisposeAsync().ConfigureAwait(false);
            }
        }

        await _network.DisposeAsync().ConfigureAwait(false);
        if (Directory.Exists(_tlsDirectory))
        {
            Directory.Delete(_tlsDirectory, recursive: true);
        }
    }

    /// <summary>Waits for a physical standby to replay a placement binding.</summary>
    /// <param name="binding">The expected binding.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The bounded wait task.</returns>
    public async Task WaitForStandbyBindingAsync(PlacementBinding binding, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        while (true)
        {
            var connection = new NpgsqlConnection(DedicatedStandby.AdminConnectionString);
            await using var connectionLifetime = connection.ConfigureAwait(false);
            await connection.OpenAsync(deadline.Token).ConfigureAwait(false);
            var command = connection.CreateCommand();
            await using var commandLifetime = command.ConfigureAwait(false);
            command.CommandText = StandbyBindingSql;
            _ = command.Parameters.AddWithValue(binding.TenantId);
            _ = command.Parameters.AddWithValue(binding.OrderId);
            _ = command.Parameters.AddWithValue(binding.SubscriptionId);
            _ = command.Parameters.AddWithValue(binding.PlacementId.ToString("D"));
            var reader = await command.ExecuteReaderAsync(deadline.Token).ConfigureAwait(false);
            await using var readerLifetime = reader.ConfigureAwait(false);
            if (await reader.ReadAsync(deadline.Token).ConfigureAwait(false)
                && reader.GetBoolean(0)
                && reader.GetBoolean(1))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), deadline.Token).ConfigureAwait(false);
        }
    }

    private static string Connection(PostgresEndpoint endpoint, string user, string password) =>
        new NpgsqlConnectionStringBuilder
        {
            Host = endpoint.Host,
            Port = endpoint.Port,
            Database = endpoint.Database,
            Username = user,
            Password = password,
            SslMode = SslMode.Require,
            Timeout = 10,
            CommandTimeout = 10,
        }.ConnectionString;

    private static async Task CreateLoginRoleAsync(
        NpgsqlConnection connection,
        string role,
        string password,
        CancellationToken cancellationToken)
    {
        var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transactionLifetime = transaction.ConfigureAwait(false);
        await SetRoleSettingsAsync(connection, transaction, role, password, cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        await using var commandLifetime = command.ConfigureAwait(false);
        command.Transaction = transaction;
        command.CommandText = CreateLoginRoleSql;
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task CreateReplicationRoleAsync(
        NpgsqlConnection connection,
        string role,
        string password,
        CancellationToken cancellationToken)
    {
        var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transactionLifetime = transaction.ConfigureAwait(false);
        await SetRoleSettingsAsync(connection, transaction, role, password, cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        await using var commandLifetime = command.ConfigureAwait(false);
        command.Transaction = transaction;
        command.CommandText = CreateReplicationRoleSql;
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task SetRoleSettingsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string role,
        string password,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using var commandLifetime = command.ConfigureAwait(false);
        command.Transaction = transaction;
        command.CommandText = SetRoleSettingsSql;
        _ = command.Parameters.AddWithValue(role);
        _ = command.Parameters.AddWithValue(password);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private PostgreSqlContainer CreateContainer(string name, string database) =>
        new PostgreSqlBuilder("postgres:18-alpine")
            .WithNetwork(_network)
            .WithNetworkAliases(name)
            .WithDatabase(database)
            .WithUsername(User)
            .WithPassword(_password)
            .WithResourceMapping(new DirectoryInfo(_tlsDirectory), "/tmp/commerce-tls")
            .WithCommand(new OverwriteEnumerable<string>(["bash", "-c", StartScript]))
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(5432))
            .Build();

    private PostgreSqlContainer CreateStandby() =>
        new PostgreSqlBuilder("postgres:18-alpine")
            .WithNetwork(_network)
            .WithNetworkAliases("commerce-dedicated-standby")
            .WithDatabase("dedicated_tenant")
            .WithUsername(User)
            .WithPassword(_password)
            .WithResourceMapping(new DirectoryInfo(_tlsDirectory), "/tmp/commerce-tls")
            .WithCommand(new OverwriteEnumerable<string>(["bash", "-c", StandbyScript]))
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(5432))
            .Build();

    private async Task InitializeSharedAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(SharedTarget.AdminConnectionString);
        await using var connectionLifetime = connection.ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await CreateLoginRoleAsync(connection, "fixture_tenant_one", _tenantOnePassword, cancellationToken).ConfigureAwait(false);
        await CreateLoginRoleAsync(connection, "fixture_tenant_two", _tenantTwoPassword, cancellationToken).ConfigureAwait(false);
        await CreateLoginRoleAsync(connection, "commerce_marker_reader", _password, cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        await using var commandLifetime = command.ConfigureAwait(false);
        command.CommandText = SharedTargetSetupSql;
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task InitializeDedicatedAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(DedicatedTarget.AdminConnectionString);
        await using var connectionLifetime = connection.ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await CreateLoginRoleAsync(connection, "commerce_marker_reader", _password, cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        await using var commandLifetime = command.ConfigureAwait(false);
        command.CommandText = DedicatedTargetSetupSql;
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private PostgresEndpoint Endpoint(PostgreSqlContainer container, string database)
    {
        var host = container.Hostname;
        var port = container.GetMappedPublicPort(5432);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = database,
            Username = User,
            Password = _password,
            SslMode = SslMode.Require,
            Timeout = 10,
            CommandTimeout = 10,
        };
        var admin = new NpgsqlConnectionStringBuilder(builder.ConnectionString)
        {
            Username = User,
            Password = _password,
        };
        return new PostgresEndpoint(builder.ConnectionString, admin.ConnectionString, host, port, database);
    }

    private void CreateTlsFiles()
    {
        Directory.CreateDirectory(_tlsDirectory);
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=commerce-fixture", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            certificateAuthority: false,
            hasPathLengthConstraint: false,
            pathLengthConstraint: 0,
            critical: false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            critical: false));
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        names.AddDnsName("commerce-dedicated-target");
        names.AddDnsName("commerce-dedicated-standby");
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        File.WriteAllText(Path.Combine(_tlsDirectory, "server.crt"), certificate.ExportCertificatePem());
        File.WriteAllText(Path.Combine(_tlsDirectory, "server.key"), rsa.ExportPkcs8PrivateKeyPem());
    }
}
