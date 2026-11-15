using System.Net;
using Grpc.Core;
using Identity.TestHost.Contracts;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using SharedKernel.Infrastructure.MultiTenancy;

namespace Identity.TestHost;

internal static class GatewayEndpoints
{
    internal static void Map(WebApplication app)
    {
        _ = app.MapGet("/proof/pkce", (PkceEvidence evidence) => Results.Json(evidence.CodeVerifierPresent)).RequireAuthorization();
        _ = app.MapGet("/login/{organization}", (string organization) => Results.Challenge(
            new AuthenticationProperties
            {
                RedirectUri = "/",
                Items = { ["organization"] = organization },
            },
            [OpenIdConnectDefaults.AuthenticationScheme])).AllowAnonymous();
        _ = app.MapGet("/", (HttpContext http, IAntiforgery antiforgery) =>
        {
            var token = antiforgery.GetAndStoreTokens(http).RequestToken ?? throw new InvalidOperationException("Antiforgery token is unavailable.");
            return Results.Content(Page(WebUtility.HtmlEncode(token)), "text/html");
        }).RequireAuthorization();
        _ = app.MapGet("/api/records/{key}", async (string key, Records.RecordsClient client, CancellationToken cancellation) =>
        {
            try
            {
                using var call = client.ReadAsync(new RecordRequest { Key = key }, deadline: DateTime.UtcNow.AddSeconds(20), cancellationToken: cancellation);
                var record = await call.ResponseAsync.ConfigureAwait(false);
                return Results.Text(record.Value);
            }
            catch (RpcException error)
            {
                return Failure(error);
            }
        }).RequireAuthorization().WithMetadata(OperationScope.Tenant);
        MapAdministrativeProbe(app);
        MapWrite(app);
        SessionEndpoints.Map(app);
    }

    private static void MapAdministrativeProbe(WebApplication app)
    {
        var administrator = string.Equals(app.Configuration["Identity:ClientId"], "identity-admin-gateway", StringComparison.Ordinal);
        var route = administrator ? "/api/admin/{tenantId}/{key}" : "/proof/untrusted-administration/{tenantId}/{key}";
        var endpoint = app.MapGet(route, async (string tenantId, string key, AdminRecords.AdminRecordsClient client, CancellationToken cancellation) =>
        {
            try
            {
                using var call = client.ReadAsync(new AdminRecordRequest { Key = key, TenantId = tenantId }, deadline: DateTime.UtcNow.AddSeconds(20), cancellationToken: cancellation);
                var record = await call.ResponseAsync.ConfigureAwait(false);
                return Results.Text(record.Value);
            }
            catch (RpcException error)
            {
                return Failure(error);
            }
        });
        if (administrator)
        {
            _ = endpoint.RequireAuthorization("platform-admin").WithMetadata(OperationScope.AdministrativeTenant("tenantId", "platform-admin"));
        }
        else
        {
            // Test-only attack probe: a real ordinary-client token must fail downstream despite user permissions.
            _ = endpoint.RequireAuthorization().WithMetadata(OperationScope.Tenant);
        }
    }

    private static void MapWrite(WebApplication app)
    {
        _ = app.MapPost("/api/records/{key}", async (string key, HttpContext http, IAntiforgery antiforgery, Records.RecordsClient client) =>
        {
            try
            {
                await antiforgery.ValidateRequestAsync(http).ConfigureAwait(false);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest();
            }

            var form = await http.Request.ReadFormAsync(http.RequestAborted).ConfigureAwait(false);
            try
            {
                using var call = client.StoreAsync(new RecordRequest { Key = key, Value = form["value"].ToString() }, deadline: DateTime.UtcNow.AddSeconds(20), cancellationToken: http.RequestAborted);
                var record = await call.ResponseAsync.ConfigureAwait(false);
                return Results.Text(record.Value);
            }
            catch (RpcException error)
            {
                return Failure(error);
            }
        }).RequireAuthorization().WithMetadata(OperationScope.Tenant);
    }

    private static IResult Failure(RpcException error) => Results.StatusCode((int)error.StatusCode switch
    {
        (int)StatusCode.Unauthenticated => StatusCodes.Status401Unauthorized,
        (int)StatusCode.PermissionDenied => StatusCodes.Status403Forbidden,
        (int)StatusCode.NotFound => StatusCodes.Status404NotFound,
        _ => StatusCodes.Status502BadGateway,
    });

    private static string Page(string token) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "index.html")).Replace("{{token}}", token, StringComparison.Ordinal);
}
