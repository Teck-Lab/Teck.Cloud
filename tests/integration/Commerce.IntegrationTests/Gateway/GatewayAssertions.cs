using System.Net;
using System.Text.Json;
using Shouldly;

namespace Commerce.IntegrationTests.Gateway;

/// <summary>Contract-shaped assertions over customer gateway success and problem responses.</summary>
internal static class GatewayAssertions
{
    internal static void ShouldBeProblem(this CommerceHttpResponse response, int status, string code)
    {
        ((int)response.Status).ShouldBe(status);
        response.MediaType.ShouldBe("application/problem+json");
        response.Root.GetProperty("status").GetInt32().ShouldBe(status);
        response.Root.GetProperty("code").GetString().ShouldBe(code);
        response.Body.ShouldNotContain("sanitized fake dependency failure");
    }

    internal static void ShouldNotContainOrderProjection(this CommerceHttpResponse response, params Guid[] orderIds)
    {
        foreach (var property in new[] { "order", "items", "checkoutUrl", "nextCursor", "license", "orderId", "subscriptionId" })
        {
            ContainsProperty(response.Root, property).ShouldBeFalse($"a failure body must not expose '{property}'");
        }

        foreach (var orderId in orderIds)
        {
            ContainsString(response.Root, orderId.ToString("D")).ShouldBeFalse("a failure body must not expose a scoped order identifier");
        }

        response.Body.ShouldNotContain(CommerceFixtures.LicenseDocument);
    }

    internal static JsonElement Order(this CommerceHttpResponse response, GatewaySurface surface, Guid orderId)
    {
        var key = orderId.ToString("D");
        return surface switch
        {
            GatewaySurface.Detail => response.Root,
            GatewaySurface.List => response.Root.GetProperty("items").EnumerateArray()
                .Single(item => string.Equals(item.GetProperty("orderId").GetString(), key, StringComparison.Ordinal)),
            GatewaySurface.Replay => response.Root.GetProperty("order"),
            _ => throw new ArgumentOutOfRangeException(nameof(surface)),
        };
    }

    internal static void ShouldBeActiveOrder(this JsonElement order, Guid orderId, Guid subscriptionId, uint revision)
    {
        order.GetProperty("orderId").GetString().ShouldBe(orderId.ToString("D"));
        order.GetProperty("subscriptionId").GetString().ShouldBe(subscriptionId.ToString("D"));
        order.GetProperty("status").GetString().ShouldBe("active");
        order.GetProperty("revision").GetInt32().ShouldBe((int)revision);
        order.TryGetProperty("desiredRevision", out _).ShouldBeFalse("desired generation never appears on HTTP");
        var license = order.GetProperty("license");
        license.ValueKind.ShouldBe(JsonValueKind.Object);
        license.GetProperty("document").GetString().ShouldBe(CommerceFixtures.LicenseDocument);
        license.GetProperty("keyId").GetString().ShouldBe(CommerceFixtures.LicenseKeyId);
        license.GetProperty("revision").GetInt32().ShouldBe((int)CommerceFixtures.LicenseRevision);
        DateTimeOffset.Parse(
            license.GetProperty("expiresAt").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind).ShouldBe(CommerceFixtures.LicenseExpiresAt);
    }

    internal static void ShouldHaveNoLicense(this JsonElement order)
    {
        var license = order.GetProperty("license");
        license.ValueKind.ShouldBe(JsonValueKind.Null);
        order.TryGetProperty("desiredRevision", out _).ShouldBeFalse("desired generation never appears on HTTP");
    }

    internal static void ShouldBePendingReadiness(this JsonElement order, uint revision, DateTimeOffset updatedAt)
    {
        order.GetProperty("status").GetString().ShouldBe("pendingReadiness");
        order.GetProperty("revision").GetInt32().ShouldBe((int)revision);
        DateTimeOffset.Parse(
            order.GetProperty("updatedAt").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind).ShouldBe(updatedAt);
        order.ShouldHaveNoLicense();
    }

    internal static void ShouldBeActiveSuccess(
        this CommerceHttpResponse response,
        GatewaySurface surface,
        JsonElement order,
        Guid orderId,
        Guid subscriptionId,
        uint revision)
    {
        response.MediaType.ShouldBe("application/json");
        switch (surface)
        {
            case GatewaySurface.Detail:
            case GatewaySurface.List:
                response.Status.ShouldBe(HttpStatusCode.OK);
                break;
            case GatewaySurface.Replay:
                response.Status.ShouldBe(HttpStatusCode.Created);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(surface));
        }

        order.ShouldBeActiveOrder(orderId, subscriptionId, revision);
        if (surface == GatewaySurface.Replay)
        {
            response.Root.TryGetProperty("checkoutUrl", out var checkoutUrl).ShouldBeTrue();
            checkoutUrl.ValueKind.ShouldBe(JsonValueKind.Null);
        }

        if (surface == GatewaySurface.List)
        {
            response.Root.TryGetProperty("nextCursor", out var cursor).ShouldBeTrue();
            cursor.GetString().ShouldBe(CommerceFixtures.Cursor);
        }
    }

    internal static void ShouldBeListPage(this CommerceHttpResponse response, IReadOnlyList<string> orderIds, string? nextCursor)
    {
        response.Status.ShouldBe(HttpStatusCode.OK);
        response.MediaType.ShouldBe("application/json");
        var items = response.Root.GetProperty("items").EnumerateArray().ToArray();
        items.Select(item => item.GetProperty("orderId").GetString()).ToArray().ShouldBe([.. orderIds]);
        response.Root.TryGetProperty("nextCursor", out var cursor).ShouldBeTrue();
        if (nextCursor is null)
        {
            cursor.ValueKind.ShouldBe(JsonValueKind.Null);
        }
        else
        {
            cursor.GetString().ShouldBe(nextCursor);
        }
    }

    private static bool ContainsProperty(JsonElement element, string property) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Any(entry =>
            string.Equals(entry.Name, property, StringComparison.Ordinal) || ContainsProperty(entry.Value, property)),
        JsonValueKind.Array => element.EnumerateArray().Any(item => ContainsProperty(item, property)),
        JsonValueKind.Undefined => false,
        JsonValueKind.String => false,
        JsonValueKind.Number => false,
        JsonValueKind.True => false,
        JsonValueKind.False => false,
        JsonValueKind.Null => false,
        _ => false,
    };

    private static bool ContainsString(JsonElement element, string value) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Any(entry => ContainsString(entry.Value, value)),
        JsonValueKind.Array => element.EnumerateArray().Any(item => ContainsString(item, value)),
        JsonValueKind.String => string.Equals(element.GetString(), value, StringComparison.Ordinal),
        JsonValueKind.Undefined => false,
        JsonValueKind.Number => false,
        JsonValueKind.True => false,
        JsonValueKind.False => false,
        JsonValueKind.Null => false,
        _ => false,
    };
}
