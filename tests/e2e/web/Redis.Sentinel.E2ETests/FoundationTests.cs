using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Aspire.Hosting.Testing;
using Npgsql;
using RabbitMQ.Client;
using Shouldly;
using Xunit;

namespace Redis.Sentinel.E2ETests;

/// <summary>Proves the default AppHost dependencies are usable, not merely started.</summary>
public sealed class FoundationTests
{
    /// <summary>Exercises SQL, broker delivery and the imported OpenID Connect realm.</summary>
    /// <returns>The verification task.</returns>
    /// <exception cref="InvalidOperationException">The shared container address is missing.</exception>
    [Fact]
    public async Task DevelopmentStackSupportsDatabaseMessagesAndIdentity()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(5));
        var host = Environment.GetEnvironmentVariable("TECK_REDIS_ADVERTISED_HOST")
            ?? throw new InvalidOperationException("Set TECK_REDIS_ADVERTISED_HOST for the container runtime.");
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Teck_Cloud_AppHost>(
            ["--Profile=development", "--Parameters:redis-advertised-host=" + host, "--Parameters:redis-password=" + password], deadline.Token).ConfigureAwait(true);
        await using var app = await builder.BuildAsync(deadline.Token).ConfigureAwait(true);
        await app.StartAsync(deadline.Token).ConfigureAwait(true);
        _ = await Task.WhenAll(
            app.ResourceNotifications.WaitForResourceHealthyAsync("postgres", deadline.Token),
            app.ResourceNotifications.WaitForResourceHealthyAsync("rabbitmq", deadline.Token),
            app.ResourceNotifications.WaitForResourceHealthyAsync("keycloak", deadline.Token)).ConfigureAwait(true);
        await VerifyDatabaseAsync((await app.GetConnectionStringAsync("application-db", deadline.Token).ConfigureAwait(true))!, deadline.Token).ConfigureAwait(true);
        await VerifyBrokerAsync((await app.GetConnectionStringAsync("rabbitmq", deadline.Token).ConfigureAwait(true))!, deadline.Token).ConfigureAwait(true);
        using var client = app.CreateHttpClient("keycloak", "http");
        using var discovery = await client.GetFromJsonAsync<JsonDocument>("/realms/teck-local/.well-known/openid-configuration", deadline.Token).ConfigureAwait(true);
        _ = discovery.ShouldNotBeNull();
        discovery.RootElement.GetProperty("issuer").GetString().ShouldEndWith("/realms/teck-local");
        discovery.RootElement.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(item => item.GetString()).Contains("S256", StringComparer.Ordinal).ShouldBeTrue();
    }

    private static async Task VerifyDatabaseAsync(string configuration, CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(configuration);
        await using var disposeConnection = connection.ConfigureAwait(true);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(true);
        var command = connection.CreateCommand();
        await using var disposeCommand = command.ConfigureAwait(true);
        command.CommandText = "CREATE TEMP TABLE proof (value text); INSERT INTO proof VALUES ('stored'); SELECT value FROM proof;";
        (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(true)).ShouldBe("stored");
    }

    private static async Task VerifyBrokerAsync(string configuration, CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory { Uri = new Uri(configuration) };
        var connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(true);
        await using var disposeConnection = connection.ConfigureAwait(true);
        var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(true);
        await using var disposeChannel = channel.ConfigureAwait(true);
        var queue = await channel.QueueDeclareAsync(cancellationToken: cancellationToken).ConfigureAwait(true);
        await channel.BasicPublishAsync(string.Empty, queue.QueueName, "delivered"u8.ToArray(), cancellationToken).ConfigureAwait(true);
        BasicGetResult? result;
        do
        {
            result = await channel.BasicGetAsync(queue.QueueName, autoAck: true, cancellationToken).ConfigureAwait(true);
            if (result is null)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(true);
            }
        }
        while (result is null);

        System.Text.Encoding.UTF8.GetString(result.Body.Span).ShouldBe("delivered");
    }
}
