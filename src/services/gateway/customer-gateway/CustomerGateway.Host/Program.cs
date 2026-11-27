using Teck.Cloud.Commerce.CustomerGateway.Host;

var application = CustomerGatewayHost.Build(args);
await application.RunAsync().ConfigureAwait(false);
