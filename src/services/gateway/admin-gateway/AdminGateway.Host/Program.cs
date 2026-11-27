using Teck.Cloud.Commerce.AdminGateway.Host;

var application = AdminGatewayHost.Build(args);
await application.RunAsync().ConfigureAwait(false);
