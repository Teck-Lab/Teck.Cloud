using Serilog;
using Serilog.Enrichers.Sensitive;
using Serilog.Exceptions;
using Serilog.Exceptions.Core;
using Serilog.Exceptions.EntityFrameworkCore.Destructurers;
using Serilog.Exceptions.Grpc.Destructurers;

namespace SharedKernel.Infrastructure.Hosting;

internal static class LogEnrichment
{
    public static void Configure(LoggerConfiguration logger)
    {
        var exceptions = new DestructuringOptionsBuilder().WithoutReflectionBasedDestructurer()
            .WithDefaultDestructurers()
            .WithFilter(new ExceptionPropertyFilter())
            .WithDestructurers([new DbUpdateExceptionDestructurer(), new RpcExceptionDestructurer()]);
        _ = logger.Enrich.WithProcessId().Enrich.WithProcessName()
            .Enrich.WithThreadId().Enrich.WithThreadName()
            .Enrich.WithExceptionDetails(exceptions);
    }

    public static void ConfigureMasking(SensitiveDataEnricherOptions options)
    {
        foreach (var name in new[] { "Password", "Pwd", "Token", "AccessToken", "Access_Token", "RefreshToken", "Refresh_Token", "IdToken", "Id_Token", "Authorization", "Proxy-Authorization", "Cookie", "Set-Cookie", "ApiKey", "Api_Key", "X-Api-Key", "Secret", "ClientSecret", "Client_Secret", "ConnectionString", "PrivateKey" })
        {
            options.MaskProperties.Add(MaskProperty.WithDefaults(name));
        }
    }
}
