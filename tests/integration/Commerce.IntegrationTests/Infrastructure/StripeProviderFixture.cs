using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>Signed Stripe-shaped HTTP provider used by the real Stripe.NET adapter.</summary>
public sealed class StripeProviderFixture : IAsyncLifetime
{
    /// <summary>The configured shared monthly price identifier.</summary>
    public const string SharedPriceId = "price_shared_fixture";

    /// <summary>The configured dedicated monthly price identifier.</summary>
    public const string DedicatedPriceId = "price_dedicated_fixture";

    /// <summary>The configured replica add-on price identifier.</summary>
    public const string ReplicaPriceId = "price_replica_fixture";

    /// <summary>The configured provider account identifier.</summary>
    public const string AccountId = "acct_commerce_fixture";

    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<string, ProviderState> _byOrder = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProviderState> _bySession = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProviderState> _bySubscription = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProviderState> _byInvoice = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _idempotentSessions = new(StringComparer.Ordinal);
    private readonly Lock _sync = new();
    private WebApplication? _app;

    /// <summary>Gets the provider API base URL.</summary>
    public Uri BaseUrl { get; private set; } = null!;

    /// <summary>Gets the webhook signing secret.</summary>
    public string WebhookSecret { get; } = "whsec_" + RandomNumberGenerator.GetHexString(32, lowercase: true);

    /// <summary>Gets the API secret key.</summary>
    public string ApiKey { get; } = "sk_test_" + RandomNumberGenerator.GetHexString(32, lowercase: true);

    /// <summary>Gets a fixed base instant for provider timestamps.</summary>
    public DateTimeOffset Now { get; } = DateTimeOffset.Parse("2026-11-27T18:00:00Z", CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.Logging.ClearProviders();
        _ = builder.WebHost.UseUrls(FreePort.Http(FreePort.Next()).ToString());
        _app = builder.Build();
        _ = _app.MapGet("/v1/prices/{id}", (string id) => PriceJson(id) is { } json ? Results.Content(json, "application/json") : StripeError("resource_missing", status: 404));
        _ = _app.MapPost("/v1/checkout/sessions", CreateSessionAsync);
        _ = _app.MapGet("/v1/checkout/sessions/{id}", (string id) => SessionJson(id) is { } json ? Results.Content(json, "application/json") : StripeError("resource_missing", status: 404));
        _ = _app.MapGet("/v1/checkout/sessions", (HttpRequest request) => Results.Content(SessionListJson(request.Query["subscription"].ToString()), "application/json"));
        _ = _app.MapGet("/v1/subscriptions/{id}", (string id) => SubscriptionJson(id) is { } json ? Results.Content(json, "application/json") : StripeError("resource_missing", status: 404));
        _ = _app.MapGet("/v1/subscriptions/search", (HttpRequest request) => Results.Content(SubscriptionSearchJson(request.Query["query"].ToString()), "application/json"));
        _ = _app.MapGet("/v1/invoices/{id}", (string id) => InvoiceJson(id) is { } json ? Results.Content(json, "application/json") : StripeError("resource_missing", status: 404));
        _ = _app.MapGet("/v1/invoices", (HttpRequest request) => Results.Content(InvoiceListJson(request.Query["subscription"].ToString()), "application/json"));
        _ = _app.MapGet("/v1/charges/{id}", (string id) => ChargeJson(id) is { } json ? Results.Content(json, "application/json") : StripeError("resource_missing", status: 404));
        _ = _app.MapGet("/v1/disputes", (HttpRequest request) => Results.Content(DisputeListJson(request.Query["charge"].ToString()), "application/json"));
        _ = _app.MapGet("/v1/customers/{id}", (string id) => Results.Content(CustomerJson(id), "application/json"));
        _ = _app.MapGet("/v1/refunds", (HttpRequest request) => Results.Content(RefundListJson(request.Query["charge"].ToString()), "application/json"));
        await _app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        BaseUrl = new Uri(_app.Urls.Single());
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        var app = _app;
        if (app is null)
        {
            return;
        }

        using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await app.StopAsync(shutdown.Token).ConfigureAwait(false);
        await app.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Gets the committed checkout state for an order.</summary>
    /// <param name="orderId">The order identifier.</param>
    /// <returns>The provider state.</returns>
    public ProviderState State(Guid orderId)
    {
        lock (_sync)
        {
            return _byOrder[orderId.ToString("D")];
        }
    }

    /// <summary>Marks the provider session and invoice paid for the current period.</summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="periodEnd">The authoritative paid-through instant.</param>
    public void MarkPaid(Guid orderId, DateTimeOffset periodEnd)
    {
        lock (_sync)
        {
            var state = _byOrder[orderId.ToString("D")] with { Paid = true, PeriodEnd = periodEnd, Refunded = false };
            Update(state);
        }
    }

    /// <summary>Marks the provider session and invoice unpaid.</summary>
    /// <param name="orderId">The order identifier.</param>
    public void MarkUnpaid(Guid orderId)
    {
        lock (_sync)
        {
            Update(_byOrder[orderId.ToString("D")] with { Paid = false, PeriodEnd = null });
        }
    }

    /// <summary>Marks the provider invoice refunded.</summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="refunded">The provider fact.</param>
    public void SetRefunded(Guid orderId, bool refunded)
    {
        lock (_sync)
        {
            Update(_byOrder[orderId.ToString("D")] with { Refunded = refunded });
        }
    }

    /// <summary>Marks the provider charge disputed.</summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="disputed">The provider fact.</param>
    public void SetDisputed(Guid orderId, bool disputed)
    {
        lock (_sync)
        {
            Update(_byOrder[orderId.ToString("D")] with { Disputed = disputed });
        }
    }

    /// <summary>Counts checkout-session create attempts for an order.</summary>
    /// <param name="orderId">The order identifier.</param>
    /// <returns>The provider create count.</returns>
    public int CreateCount(Guid orderId)
    {
        lock (_sync)
        {
            return _byOrder.TryGetValue(orderId.ToString("D"), out var state) ? state.CreateCount : 0;
        }
    }

    /// <summary>Creates a signed Stripe webhook payload for the order's current binding.</summary>
    /// <param name="eventId">The provider event identifier.</param>
    /// <param name="type">The provider event type.</param>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="objectType">The event object type.</param>
    /// <returns>The raw JSON body.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The event object type is not supported.</exception>
    /// <exception cref="InvalidOperationException">The provider object is missing.</exception>
    public string WebhookPayload(string eventId, string type, Guid orderId, string objectType)
    {
        var state = State(orderId);
        var data = objectType switch
        {
            "checkout.session" => SessionJson(state.SessionId),
            "invoice" => InvoiceJson(state.InvoiceId),
            "subscription" => SubscriptionJson(state.SubscriptionId),
            _ => throw new ArgumentOutOfRangeException(nameof(objectType)),
        };
        var root = new JsonObject
        {
            ["id"] = eventId,
            ["object"] = "event",
            ["api_version"] = "2025-10-29.clover",
            ["created"] = Now.ToUnixTimeSeconds(),
            ["livemode"] = false,
            ["type"] = type,
            ["account"] = AccountId,
            ["request"] = null,
            ["data"] = new JsonObject
            {
                ["object"] = JsonNode.Parse(data ?? throw new InvalidOperationException("The provider object is missing.")),
            },
        };
        return root.ToJsonString(_json);
    }

    /// <summary>Signs a raw webhook body with the configured endpoint secret.</summary>
    /// <param name="rawBody">The exact raw body.</param>
    /// <param name="timestamp">The signature timestamp.</param>
    /// <returns>The Stripe-Signature header value.</returns>
    public string SignatureHeader(string rawBody, DateTimeOffset timestamp)
    {
        var signed = timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) + "." + rawBody;
        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(WebhookSecret), Encoding.UTF8.GetBytes(signed));
        return "t=" + timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) + ",v1=" + Convert.ToHexStringLower(digest);
    }

    private static JsonObject InvoicePaymentsNode(ProviderState state) => new()
    {
        ["object"] = "list",
        ["has_more"] = false,
        ["data"] = new JsonArray(new JsonObject
        {
            ["id"] = "inpay_" + state.OrderId.ToString("N"),
            ["object"] = "invoice_payment",
            ["status"] = state.Paid ? "paid" : "open",
            ["payment"] = new JsonObject
            {
                ["type"] = "charge",
                ["charge"] = "ch_" + state.OrderId.ToString("N"),
                ["payment_intent"] = "pi_" + state.OrderId.ToString("N"),
            },
        }),
    };

    private static JsonObject InvoiceLinesNode(ProviderState state, long start, long end)
    {
        var data = new JsonArray();
        for (var index = 0; index < state.PriceIds.Count; index++)
        {
            var priceId = state.PriceIds[index];
            var amount = Amount(priceId);
            data.Add(new JsonObject
            {
                ["id"] = "il_" + state.OrderId.ToString("N") + "_" + index.ToString(CultureInfo.InvariantCulture),
                ["object"] = "line_item",
                ["amount"] = amount,
                ["currency"] = "usd",
                ["period"] = new JsonObject { ["start"] = start, ["end"] = end },
                ["quantity"] = 1,
                ["pricing"] = new JsonObject
                {
                    ["type"] = "price_details",
                    ["price_details"] = new JsonObject
                    {
                        ["price"] = priceId,
                        ["product"] = "prod_commerce_fixture",
                    },
                    ["unit_amount_decimal"] = amount.ToString(CultureInfo.InvariantCulture),
                },
            });
        }

        return new JsonObject
        {
            ["object"] = "list",
            ["has_more"] = false,
            ["data"] = data,
        };
    }

    private static JsonObject PriceNode(string id) => new()
    {
        ["id"] = id,
        ["object"] = "price",
        ["active"] = true,
        ["type"] = "recurring",
        ["currency"] = "usd",
        ["unit_amount"] = Amount(id),
        ["recurring"] = new JsonObject { ["interval"] = "month", ["interval_count"] = 1 },
        ["livemode"] = false,
        ["product"] = "prod_commerce_fixture",
    };

    private static JsonArray SubscriptionItemsNode(ProviderState state, long start, long end)
    {
        var data = new JsonArray();
        for (var index = 0; index < state.PriceIds.Count; index++)
        {
            data.Add(new JsonObject
            {
                ["id"] = "si_" + state.OrderId.ToString("N") + "_" + index.ToString(CultureInfo.InvariantCulture),
                ["object"] = "subscription_item",
                ["quantity"] = 1,
                ["price"] = PriceNode(state.PriceIds[index]),
                ["current_period_start"] = start,
                ["current_period_end"] = end,
            });
        }

        return data;
    }

    private static long Amount(string priceId) => priceId switch
    {
        SharedPriceId => 1000,
        DedicatedPriceId => 3000,
        ReplicaPriceId => 500,
        _ => 0,
    };

    private static List<string> ReadLinePrices(IFormCollection form)
    {
        var lineFields = form.Keys.Count(key => key.StartsWith("line_items[", StringComparison.Ordinal));
        if (lineFields % 2 != 0)
        {
            return [];
        }

        var prices = new List<string>(lineFields / 2);
        for (var index = 0; index < lineFields / 2; index++)
        {
            var field = "line_items[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            var price = form[field + "[price]"];
            var quantity = form[field + "[quantity]"];
            if (price.Count != 1 || quantity.Count != 1 || !string.Equals(quantity[0], "1", StringComparison.Ordinal)
                || price[0] is not { } priceId || Amount(priceId) == 0)
            {
                return [];
            }

            prices.Add(priceId);
        }

        return prices;
    }

    private static string CustomerJson(string id) => new JsonObject
    {
        ["id"] = id,
        ["object"] = "customer",
        ["livemode"] = false,
    }.ToJsonString();

    private static IResult StripeError(string code, string? parameter = null, int status = 400) =>
        Results.Json(new { error = new { type = "invalid_request_error", code, param = parameter } }, statusCode: status);

    private void Update(ProviderState state)
    {
        _byOrder[state.OrderId.ToString("D")] = state;
        _bySession[state.SessionId] = state;
        _bySubscription[state.SubscriptionId] = state;
        _byInvoice[state.InvoiceId] = state;
    }

    private async Task<IResult> CreateSessionAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        var orderValue = form["client_reference_id"].ToString();
        if (string.IsNullOrWhiteSpace(orderValue))
        {
            orderValue = form["metadata[orderId]"].ToString();
        }

        if (!Guid.TryParse(orderValue, out var orderId))
        {
            return StripeError("parameter_missing", "client_reference_id");
        }

        var priceIds = ReadLinePrices(form);
        return priceIds.Count == 0
            ? StripeError("parameter_invalid", "line_items")
            : CreateSessionFromForm(request, form, orderId, priceIds);
    }

    private IResult CreateSessionFromForm(HttpRequest request, IFormCollection form, Guid orderId, List<string> priceIds)
    {
        return Guid.TryParse(form["metadata[tenantId]"].ToString(), out var tenantId)
            && Guid.TryParse(form["metadata[subscriptionId]"].ToString(), out var commerceSubscriptionId)
            ? CreateSession(orderId, tenantId, commerceSubscriptionId, priceIds, request.Headers["Idempotency-Key"].ToString())
            : StripeError("parameter_invalid", "metadata");
    }

    private IResult CreateSession(Guid orderId, Guid tenantId, Guid commerceSubscriptionId, List<string> priceIds, string idempotencyKey)
    {
        var amount = priceIds.Sum(Amount);
        lock (_sync)
        {
            if (_byOrder.TryGetValue(orderId.ToString("D"), out var existing))
            {
                return !string.IsNullOrEmpty(idempotencyKey)
                    && _idempotentSessions.TryGetValue(idempotencyKey, out var mapped)
                    && !string.Equals(mapped, existing.SessionId, StringComparison.Ordinal)
                    ? StripeError("idempotency_error")
                    : Results.Content(SessionJson(existing), "application/json");
            }

            var id = "cs_test_" + orderId.ToString("N");
            var state = new ProviderState(
                orderId,
                priceIds[0],
                amount,
                "cus_test_" + orderId.ToString("N"),
                id,
                "sub_test_" + orderId.ToString("N"),
                "in_test_" + orderId.ToString("N"),
                Paid: false,
                PeriodEnd: null,
                Refunded: false,
                CreateCount: 1)
            {
                PriceIds = priceIds,
                TenantId = tenantId,
                CommerceSubscriptionId = commerceSubscriptionId,
                Disputed = false,
            };
            _byOrder[orderId.ToString("D")] = state;
            _bySession[id] = state;
            _bySubscription[state.SubscriptionId] = state;
            _byInvoice[state.InvoiceId] = state;
            if (!string.IsNullOrEmpty(idempotencyKey))
            {
                _idempotentSessions[idempotencyKey] = id;
            }

            return Results.Content(SessionJson(state), "application/json");
        }
    }

    private string? SessionJson(string id)
    {
        lock (_sync)
        {
            return _bySession.TryGetValue(id, out var state) ? SessionJson(state) : null;
        }
    }

    private string SessionJson(ProviderState state) => new JsonObject
    {
        ["id"] = state.SessionId,
        ["object"] = "checkout.session",
        ["mode"] = "subscription",
        ["status"] = state.Paid ? "complete" : "open",
        ["payment_status"] = state.Paid ? "paid" : "unpaid",
        ["amount_total"] = state.AmountMinor,
        ["currency"] = "usd",
        ["client_reference_id"] = state.OrderId.ToString("D"),
        ["customer"] = state.CustomerId,
        ["subscription"] = state.Paid ? state.SubscriptionId : null,
        ["invoice"] = state.Paid ? state.InvoiceId : null,
        ["url"] = "https://checkout.stripe.test/c/pay/" + state.SessionId,
        ["expires_at"] = Now.AddHours(24).ToUnixTimeSeconds(),
        ["created"] = Now.ToUnixTimeSeconds(),
        ["livemode"] = false,
        ["metadata"] = new JsonObject
        {
            ["orderId"] = state.OrderId.ToString("D"),
            ["tenantId"] = state.TenantId.ToString("D"),
            ["subscriptionId"] = state.CommerceSubscriptionId.ToString("D"),
        },
    }.ToJsonString(_json);

    private string? SubscriptionJson(string id)
    {
        lock (_sync)
        {
            if (!_bySubscription.TryGetValue(id, out var state))
            {
                return null;
            }

            var start = Now.ToUnixTimeSeconds();
            var end = (state.PeriodEnd ?? Now.AddMonths(1)).ToUnixTimeSeconds();
            return new JsonObject
            {
                ["id"] = state.SubscriptionId,
                ["object"] = "subscription",
                ["status"] = "active",
                ["customer"] = state.CustomerId,
                ["current_period_start"] = start,
                ["current_period_end"] = end,
                ["latest_invoice"] = state.InvoiceId,
                ["currency"] = "usd",
                ["livemode"] = false,
                ["metadata"] = new JsonObject
                {
                    ["orderId"] = state.OrderId.ToString("D"),
                    ["tenantId"] = state.TenantId.ToString("D"),
                    ["subscriptionId"] = state.CommerceSubscriptionId.ToString("D"),
                },
                ["items"] = new JsonObject
                {
                    ["object"] = "list",
                    ["has_more"] = false,
                    ["data"] = SubscriptionItemsNode(state, start, end),
                },
            }.ToJsonString(_json);
        }
    }

    private string? InvoiceJson(string id)
    {
        lock (_sync)
        {
            if (!_byInvoice.TryGetValue(id, out var state))
            {
                return null;
            }

            var start = Now.ToUnixTimeSeconds();
            var end = (state.PeriodEnd ?? Now.AddMonths(1)).ToUnixTimeSeconds();
            return new JsonObject
            {
                ["id"] = state.InvoiceId,
                ["object"] = "invoice",
                ["status"] = state.Paid ? "paid" : "open",
                ["paid"] = state.Paid,
                ["amount_paid"] = state.Paid ? state.AmountMinor : 0,
                ["amount_remaining"] = state.Paid ? 0 : state.AmountMinor,
                ["amount_due"] = state.Paid ? 0 : state.AmountMinor,
                ["currency"] = "usd",
                ["livemode"] = false,
                ["subscription"] = state.SubscriptionId,
                ["customer"] = state.CustomerId,
                ["period_start"] = start,
                ["period_end"] = end,
                ["status_transitions"] = new JsonObject(),
                ["parent"] = new JsonObject
                {
                    ["type"] = "subscription_details",
                    ["subscription_details"] = new JsonObject
                    {
                        ["subscription"] = state.SubscriptionId,
                        ["metadata"] = new JsonObject
                        {
                            ["orderId"] = state.OrderId.ToString("D"),
                            ["tenantId"] = state.TenantId.ToString("D"),
                            ["subscriptionId"] = state.CommerceSubscriptionId.ToString("D"),
                        },
                    },
                },
                ["payments"] = InvoicePaymentsNode(state),
                ["lines"] = InvoiceLinesNode(state, start, end),
            }.ToJsonString(_json);
        }
    }

    private string SessionListJson(string subscriptionId)
    {
        lock (_sync)
        {
            var data = _bySubscription.TryGetValue(subscriptionId, out var state) && SessionJson(state.SessionId) is { } session
                ? new JsonArray(JsonNode.Parse(session))
                : [];
            return new JsonObject
            {
                ["object"] = "list",
                ["has_more"] = false,
                ["url"] = "/v1/checkout/sessions",
                ["data"] = data,
            }.ToJsonString(_json);
        }
    }

    private string SubscriptionSearchJson(string query)
    {
        lock (_sync)
        {
            var orderId = _byOrder.Keys.FirstOrDefault(key => query.Contains(key, StringComparison.Ordinal));
            var data = orderId is not null && SubscriptionJson(_byOrder[orderId].SubscriptionId) is { } subscription
                ? new JsonArray(JsonNode.Parse(subscription))
                : [];
            return new JsonObject
            {
                ["object"] = "search_result",
                ["url"] = "/v1/subscriptions/search",
                ["has_more"] = false,
                ["data"] = data,
            }.ToJsonString(_json);
        }
    }

    private string? ChargeJson(string id)
    {
        lock (_sync)
        {
            return _byOrder.Values.FirstOrDefault(candidate => string.Equals(id, "ch_" + candidate.OrderId.ToString("N"), StringComparison.Ordinal)) is { } state
                ? ChargeJson(id, state)
                : null;
        }
    }

    private string ChargeJson(string id, ProviderState state) => new JsonObject
    {
        ["id"] = id,
        ["object"] = "charge",
        ["livemode"] = false,
        ["paid"] = state.Paid,
        ["currency"] = "usd",
        ["amount"] = state.AmountMinor,
        ["amount_refunded"] = state.Refunded ? state.AmountMinor : 0,
        ["refunded"] = state.Refunded,
        ["disputed"] = state.Disputed,
        ["payment_intent"] = "pi_" + state.OrderId.ToString("N"),
        ["metadata"] = new JsonObject { ["orderId"] = state.OrderId.ToString("D") },
    }.ToJsonString(_json);

    private string DisputeListJson(string chargeId)
    {
        lock (_sync)
        {
            var state = _byOrder.Values.FirstOrDefault(candidate => string.Equals(chargeId, "ch_" + candidate.OrderId.ToString("N"), StringComparison.Ordinal));
            var data = state is { Disputed: true }
                ? new JsonArray(new JsonObject
                {
                    ["id"] = "dp_" + state.OrderId.ToString("N"),
                    ["object"] = "dispute",
                    ["status"] = "needs_response",
                    ["charge"] = chargeId,
                    ["payment_intent"] = "pi_" + state.OrderId.ToString("N"),
                    ["currency"] = "usd",
                    ["amount"] = state.AmountMinor,
                    ["created"] = Now.ToUnixTimeSeconds(),
                    ["livemode"] = false,
                })
                : [];
            return new JsonObject
            {
                ["object"] = "list",
                ["has_more"] = false,
                ["url"] = "/v1/disputes",
                ["data"] = data,
            }.ToJsonString(_json);
        }
    }

    private string InvoiceListJson(string subscriptionId)
    {
        lock (_sync)
        {
            var data = _bySubscription.TryGetValue(subscriptionId, out var state) && InvoiceJson(state.InvoiceId) is { } invoice
                ? new JsonArray(JsonNode.Parse(invoice))
                : [];
            return new JsonObject
            {
                ["object"] = "list",
                ["has_more"] = false,
                ["url"] = "/v1/invoices",
                ["data"] = data,
            }.ToJsonString(_json);
        }
    }

    private string RefundListJson(string chargeId)
    {
        lock (_sync)
        {
            var refunded = _byOrder.Values.Any(state => state.Refunded && state.InvoiceId.Contains(chargeId, StringComparison.Ordinal));
            var data = refunded
                ? new JsonArray(new JsonObject
                {
                    ["id"] = "re_" + chargeId,
                    ["object"] = "refund",
                    ["status"] = "succeeded",
                    ["charge"] = chargeId,
                    ["amount"] = 1000,
                    ["currency"] = "usd",
                })
                : [];
            return new JsonObject
            {
                ["object"] = "list",
                ["has_more"] = false,
                ["url"] = "/v1/refunds",
                ["data"] = data,
            }.ToJsonString(_json);
        }
    }

    private string? PriceJson(string id) =>
        Amount(id) is 0 ? null : PriceNode(id).ToJsonString(_json);
}
