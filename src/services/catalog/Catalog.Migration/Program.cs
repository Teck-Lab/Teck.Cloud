using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SharedKernel.Migration;
using SharedKernel.Migration.Services;
using SharedKernel.Secrets;
using Wolverine;
using Wolverine.RabbitMQ;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Catalog Migration Service");

    var host = Host.CreateDefaultBuilder(args)
        .ConfigureServices((context, services) =>
        {
            // Configure Serilog
            services.AddSerilog();

            // Add Vault Secrets Manager
            var vaultOptions = context.Configuration.GetSection("Vault").Get<VaultOptions>()
                ?? throw new InvalidOperationException("Vault configuration is required");

            services.AddSingleton(vaultOptions);
            services.AddSingleton<IVaultSecretsManager, VaultSecretsManager>();

            // Add Customer API Client
            var customerApiUrl = context.Configuration["CustomerApi:BaseUrl"]
                ?? throw new InvalidOperationException("CustomerApi:BaseUrl configuration is required");

            services.AddHttpClient<CustomerApiClient>(client =>
            {
                client.BaseAddress = new Uri(customerApiUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            // Add DbUp Migration Runner
            services.AddSingleton<DbUpMigrationRunner>();
        })
        .UseWolverine(opts =>
        {
            // Build RabbitMQ connection URI
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables()
                .Build();

            var rabbitMqHost = config["RabbitMQ:Host"] ?? "localhost";
            var rabbitMqPort = config.GetValue<int>("RabbitMQ:Port", 5672);
            var rabbitMqUser = config["RabbitMQ:Username"] ?? "guest";
            var rabbitMqPassword = config["RabbitMQ:Password"] ?? "guest";

            var rabbitMqUri = new Uri($"amqp://{rabbitMqUser}:{rabbitMqPassword}@{rabbitMqHost}:{rabbitMqPort}");

            var rabbit = opts.UseRabbitMq(rabbitMqUri);
            rabbit.AutoProvision();
            rabbit.EnableWolverineControlQueues();

            // Listen to TenantCreatedIntegrationEvent on a specific queue
            opts.ListenToRabbitQueue("catalog.migration.tenant-created")
                .UseDurableInbox();
        })
        .Build();

    await host.RunAsync();

    return 0;
}
catch (Exception exception)
{
    Log.Fatal(exception, "Catalog Migration Service terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
