using Customer.Migration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using SharedKernel.Migration;
using SharedKernel.Migration.Services;
using SharedKernel.Secrets;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Customer Migration Service");

    var builder = Host.CreateApplicationBuilder(args);

    // Configure Serilog
    builder.Services.AddSerilog();

    // Add Vault Secrets Manager
    var vaultOptions = builder.Configuration.GetSection("Vault").Get<VaultOptions>()
        ?? throw new InvalidOperationException("Vault configuration is required");

    builder.Services.AddSingleton(vaultOptions);
    builder.Services.AddSingleton<IVaultSecretsManager, VaultSecretsManager>();

    // Add Customer API Client
    var customerApiUrl = builder.Configuration["CustomerApi:BaseUrl"]
        ?? throw new InvalidOperationException("CustomerApi:BaseUrl configuration is required");

    builder.Services.AddHttpClient<CustomerApiClient>(client =>
    {
        client.BaseAddress = new Uri(customerApiUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    // Add DbUp Migration Runner
    builder.Services.AddSingleton<DbUpMigrationRunner>();

    // Add the migration service as a hosted service
    builder.Services.AddHostedService<CustomerMigrationService>();

    var host = builder.Build();

    await host.RunAsync();

    return 0;
}
catch (Exception exception)
{
    Log.Fatal(exception, "Customer Migration Service terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
