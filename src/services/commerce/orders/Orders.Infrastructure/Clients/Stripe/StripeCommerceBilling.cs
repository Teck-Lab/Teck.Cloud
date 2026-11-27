using System.Net;
using Stripe;
using Stripe.Checkout;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Clients.Stripe;

/// <summary>
/// Stripe Billing adapter. Notifications are triggers only; every payment fact comes from a
/// server-side re-fetch that verifies the stored order, session, subscription and invoice binding.
/// </summary>
internal sealed class StripeCommerceBilling : ICommerceBilling
{
    private const string MetadataOrderId = "orderId";
    private const string MetadataTenantId = "tenantId";
    private const string MetadataSubscriptionId = "subscriptionId";
    private const string MetadataCatalogKey = "catalogKey";
    private const string SubscriptionMode = "subscription";
    private const string MonthlyInterval = "month";

    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromHours(24);

    private static readonly HashSet<string> ActiveDisputeStatuses = new(StringComparer.Ordinal)
    {
        "warning_needs_response",
        "warning_under_review",
        "needs_response",
        "under_review",
    };

    private readonly StripeCommerceOptions _options;

    private readonly TimeProvider _timeProvider;

    private readonly BillingProviderMode? _mode;

    private readonly string? _accountId;

    private readonly string? _checkoutOrigin;

    private readonly string? _successUrlTemplate;

    private readonly string? _cancelUrlTemplate;

    private readonly string? _configurationFailure;

    private readonly string? _checkoutConfigurationFailure;

    private readonly PriceService? _prices;

    private readonly SessionService? _sessions;

    private readonly SubscriptionService? _subscriptions;

    private readonly InvoiceService? _invoices;

    private readonly ChargeService? _charges;

    private readonly DisputeService? _disputes;

    internal StripeCommerceBilling(StripeCommerceOptions options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _options = options;
        _timeProvider = timeProvider;
        _mode = TryParseMode(options.Mode, out var mode) ? mode : null;
        _accountId = NormalizeOptional(options.AccountId);
        _checkoutOrigin = NormalizeOrigin(options.CheckoutOrigin);
        _successUrlTemplate = NormalizeReturnUrlTemplate(options.SuccessUrl);
        _cancelUrlTemplate = NormalizeReturnUrlTemplate(options.CancelUrl);

        var settings = ReadClientSettings(options, out var clientFailure);
        _configurationFailure = clientFailure;
        if (settings is not null)
        {
            var client = new StripeClient(settings.ApiKey, apiBase: settings.ApiBase);
            _prices = new PriceService(client);
            _sessions = new SessionService(client);
            _subscriptions = new SubscriptionService(client);
            _invoices = new InvoiceService(client);
            _charges = new ChargeService(client);
            _disputes = new DisputeService(client);
        }

        _checkoutConfigurationFailure =
            _checkoutOrigin is null || _successUrlTemplate is null || _cancelUrlTemplate is null
                ? BillingFailureCodes.InvalidConfiguration
                : null;
    }

    /// <inheritdoc/>
    public async Task<BillingPriceVerification> ValidatePriceAsync(
        BillingPrice price,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(price);

        if (!TryResolveExpectedLines(price, out var expectedLines, out var expectedCurrency))
        {
            return PriceFailure(BillingFailureCodes.InvalidConfiguration);
        }

        if (_mode is not { } mode || _configurationFailure is not null || _prices is null)
        {
            return PriceFailure(BillingFailureCodes.InvalidConfiguration);
        }

        try
        {
            return await VerifyPricesAsync(expectedLines, expectedCurrency, mode, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (StripeException ex)
        {
            return PriceFailure(ex.HttpStatusCode == HttpStatusCode.NotFound
                ? BillingFailureCodes.InvalidConfiguration
                : MapProviderFailure(ex));
        }
        catch (HttpRequestException)
        {
            return PriceFailure(BillingFailureCodes.ProviderUnavailable);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PriceFailure(BillingFailureCodes.ProviderUnavailable);
        }
    }

    /// <inheritdoc/>
    public Task<BillingCheckout> CreateAsync(BillingOrder order, CancellationToken cancellationToken)
        => CreateOrRecoverAsync(order, cancellationToken);

    /// <inheritdoc/>
    public Task<BillingCheckout> RecoverAsync(BillingOrder order, CancellationToken cancellationToken)
        => CreateOrRecoverAsync(order, cancellationToken);

    /// <inheritdoc/>
    public async Task<BillingReconciliation> ReconcileAsync(
        BillingOrder order,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);

        var now = _timeProvider.GetUtcNow();
        var state = CreateState(order, now);
        if (state is null)
        {
            return Reconciliation(now, BillingEvidenceStatus.Unavailable, _mode ?? BillingProviderMode.Test, BillingFailureCodes.InvalidConfiguration);
        }

        if (order.CheckoutSessionId is null && order.ProviderSubscriptionId is null)
        {
            return Reconciliation(now, BillingEvidenceStatus.None, state.Mode, failureCode: null);
        }

        var failure = await LoadSessionAsync(state, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(state.SubscriptionId))
        {
            return Reconciliation(now, BillingEvidenceStatus.Pending, state.Mode, failureCode: null, sessionId: state.SessionId);
        }

        failure = await LoadSubscriptionAsync(state, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        failure = await LoadInvoiceAsync(state, cancellationToken).ConfigureAwait(false);
        return failure
            ?? await LoadChargeAsync(state, cancellationToken).ConfigureAwait(false)
            ?? await EvaluateAsync(state, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public BillingNotice? ParseWebhook(string rawBody, string signatureHeader)
    {
        var providerEvent = VerifyWebhookEvent(rawBody, signatureHeader);
        return providerEvent is null ? null : MapNotice(providerEvent);
    }

    private static bool TryParseMode(string? value, out BillingProviderMode mode)
    {
        var normalized = NormalizeOptional(value);
        mode = BillingProviderMode.Test;
        if (string.Equals(normalized, "test", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(normalized, "live", StringComparison.OrdinalIgnoreCase))
        {
            mode = BillingProviderMode.Live;
            return true;
        }

        return false;
    }

    private static bool IsLive(BillingProviderMode mode) => mode == BillingProviderMode.Live;

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeCurrency(string? value)
    {
        var normalized = NormalizeOptional(value);
        return normalized is null || normalized.Length != 3 ? null : normalized.ToUpperInvariant();
    }

    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static bool TryExpectedPriceCurrency(IReadOnlyList<BillingPriceLine> lines, out string currency)
    {
        currency = string.Empty;
        var expected = lines is { Count: > 0 } ? NormalizeCurrency(lines[0].Currency) : null;
        if (expected is null || !lines.All(line =>
                !string.IsNullOrWhiteSpace(line.Code) &&
                line.UnitAmountMinor > 0 &&
                string.Equals(line.Interval, MonthlyInterval, StringComparison.OrdinalIgnoreCase) &&
                line.IntervalCount == 1 &&
                string.Equals(NormalizeCurrency(line.Currency), expected, StringComparison.Ordinal)))
        {
            return false;
        }

        currency = expected;
        return true;
    }

    private static bool TryExpectedCurrency(IReadOnlyList<BillingVerifiedPriceLine> lines, out string currency)
    {
        currency = string.Empty;
        var expected = lines is { Count: > 0 } ? NormalizeCurrency(lines[0].Currency) : null;
        if (expected is null || !lines.All(line =>
                !string.IsNullOrWhiteSpace(line.ProviderPriceId) &&
                string.Equals(NormalizeCurrency(line.Currency), expected, StringComparison.Ordinal)))
        {
            return false;
        }

        currency = expected;
        return true;
    }

    private static bool TryExpectedTotal(IReadOnlyList<BillingVerifiedPriceLine> lines, out long total)
    {
        total = 0;
        if (lines is not { Count: > 0 } || !lines.All(static line => line.UnitAmountMinor > 0))
        {
            return false;
        }

        try
        {
            total = lines.Sum(static line => line.UnitAmountMinor);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static bool IsExpectedPrice(
        Price price,
        BillingPriceLine line,
        string providerPriceId,
        string expectedCurrency,
        BillingProviderMode mode)
        => string.Equals(price.Id, providerPriceId, StringComparison.Ordinal) &&
           price.Livemode == IsLive(mode) &&
           price.Active &&
           price.Deleted != true &&
           string.Equals(price.Type, "recurring", StringComparison.Ordinal) &&
           price.Recurring is not null &&
           string.Equals(price.Recurring.Interval, MonthlyInterval, StringComparison.Ordinal) &&
           price.Recurring.IntervalCount == line.IntervalCount &&
           price.UnitAmount is long unitAmount &&
           unitAmount == line.UnitAmountMinor &&
           string.Equals(NormalizeCurrency(price.Currency), expectedCurrency, StringComparison.Ordinal);

    private static bool MetadataMatches(Dictionary<string, string>? metadata, BillingOrder order)
        => metadata is not null &&
           metadata.TryGetValue(MetadataOrderId, out var orderId) &&
           metadata.TryGetValue(MetadataTenantId, out var tenantId) &&
           metadata.TryGetValue(MetadataSubscriptionId, out var subscriptionId) &&
           string.Equals(orderId, order.OrderId.ToString("D"), StringComparison.Ordinal) &&
           string.Equals(tenantId, order.TenantId.ToString("D"), StringComparison.Ordinal) &&
           string.Equals(subscriptionId, order.SubscriptionId.ToString("D"), StringComparison.Ordinal);

    private static string? FindSingleChargeId(Invoice invoice)
    {
        var payments = invoice.Payments?.Data;
        if (payments is null)
        {
            return null;
        }

        string[] chargeIds =
        [
            .. payments
                .Where(static payment => string.Equals(payment.Status, "paid", StringComparison.Ordinal))
                .Select(static payment => payment.Payment?.ChargeId)
                .OfType<string>()
                .Where(static id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal),
        ];

        return chargeIds.Length == 1 ? chargeIds[0] : null;
    }

    private static Guid? ParseOrderId(Dictionary<string, string>? metadata)
        => metadata is not null && metadata.TryGetValue(MetadataOrderId, out var value)
            ? ParseOrderIdFromText(value)
            : null;

    private static Guid? ParseOrderIdFromText(string? value)
        => Guid.TryParse(value, out var orderId) ? orderId : null;

    private static bool IsAmbiguous(StripeException exception)
        => exception.HttpStatusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
            or HttpStatusCode.TooManyRequests;

    private static string MapProviderFailure(StripeException exception)
    {
        if (string.Equals(exception.StripeError?.Code, "idempotency_key_in_use", StringComparison.Ordinal))
        {
            return BillingFailureCodes.IdempotencyConflict;
        }

        var transient = IsAmbiguous(exception) ||
                        exception.HttpStatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
        return transient ? BillingFailureCodes.ProviderUnavailable : BillingFailureCodes.ProviderRejected;
    }

    private static string BuildIdempotencyKey(Guid orderId)
        => $"teck-cloud-commerce-checkout-v1-{orderId:N}";

    private static bool KeyMatchesMode(string apiKey, BillingProviderMode mode)
    {
        var liveKey = apiKey.StartsWith("sk_live_", StringComparison.Ordinal) ||
                      apiKey.StartsWith("rk_live_", StringComparison.Ordinal);
        var testKey = apiKey.StartsWith("sk_test_", StringComparison.Ordinal) ||
                      apiKey.StartsWith("rk_test_", StringComparison.Ordinal);
        return liveKey ? mode == BillingProviderMode.Live : !testKey || mode == BillingProviderMode.Test;
    }

    private static bool TryNormalizeApiBase(string? value, BillingProviderMode mode, out string apiBase)
    {
        apiBase = StripeCommerceOptions.DefaultApiBase;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            uri.AbsolutePath.TrimEnd('/').Length != 0)
        {
            return false;
        }

        var secure = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);
        var testLoopback = mode == BillingProviderMode.Test &&
                           string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) &&
                           uri.IsLoopback;
        if (!secure && !testLoopback)
        {
            return false;
        }

        apiBase = uri.GetLeftPart(UriPartial.Authority);
        return true;
    }

    private static string? NormalizeOrigin(string? value)
    {
        var candidate = string.IsNullOrWhiteSpace(value)
            ? StripeCommerceOptions.DefaultCheckoutOrigin
            : value.Trim();
        return Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
               IsAllowedOrigin(uri)
            ? uri.GetLeftPart(UriPartial.Authority)
            : null;
    }

    private static string? NormalizeReturnUrlTemplate(string? value)
    {
        var candidate = NormalizeOptional(value);
        return candidate is not null && IsHttpsUrl(ExpandReturnUrl(candidate, Guid.Empty))
            ? candidate
            : null;
    }

    private static string ExpandReturnUrl(string template, Guid orderId)
        => template.Replace("{orderId}", orderId.ToString("D"), StringComparison.Ordinal);

    private static bool IsAllowedCheckoutUrl(string url, string checkoutOrigin)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
           IsHttpsUrl(url) &&
           string.Equals(uri.GetLeftPart(UriPartial.Authority), checkoutOrigin, StringComparison.Ordinal);

    private static bool IsHttpsUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
           string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);

    private static bool IsAllowedOrigin(Uri uri)
        => string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) &&
           string.IsNullOrEmpty(uri.UserInfo) &&
           string.IsNullOrEmpty(uri.Query) &&
           string.IsNullOrEmpty(uri.Fragment) &&
           uri.AbsolutePath.TrimEnd('/').Length == 0;

    private static Dictionary<string, string> BuildMetadata(BillingOrder order) => new(StringComparer.Ordinal)
    {
        [MetadataOrderId] = order.OrderId.ToString("D"),
        [MetadataTenantId] = order.TenantId.ToString("D"),
        [MetadataSubscriptionId] = order.SubscriptionId.ToString("D"),
        [MetadataCatalogKey] = order.CatalogKey,
    };

    private static string? VerifySessionBinding(Session session, BillingOrder order, BillingProviderMode mode)
    {
        var mismatch = string.IsNullOrWhiteSpace(session.Id) ||
                       session.Livemode != IsLive(mode) ||
                       !string.Equals(session.Mode, SubscriptionMode, StringComparison.Ordinal) ||
                       !string.Equals(session.ClientReferenceId, order.OrderId.ToString("D"), StringComparison.Ordinal) ||
                       !MetadataMatches(session.Metadata, order) ||
                       (!string.IsNullOrWhiteSpace(session.SubscriptionId) &&
                        !string.IsNullOrWhiteSpace(order.ProviderSubscriptionId) &&
                        !string.Equals(session.SubscriptionId, order.ProviderSubscriptionId, StringComparison.Ordinal));
        return mismatch ? BillingFailureCodes.BindingMismatch : null;
    }

    private static string? VerifySubscriptionBinding(
        Subscription subscription,
        BillingOrder order,
        BillingProviderMode mode,
        string expectedCurrency)
    {
        if (subscription.Livemode != IsLive(mode) || !MetadataMatches(subscription.Metadata, order))
        {
            return BillingFailureCodes.BindingMismatch;
        }

        if (!string.Equals(NormalizeCurrency(subscription.Currency), expectedCurrency, StringComparison.Ordinal))
        {
            return BillingFailureCodes.CurrencyMismatch;
        }

        var items = subscription.Items?.Data;
        if (items is null || items.Count != order.Lines.Count)
        {
            return BillingFailureCodes.PriceMismatch;
        }

        string[] expectedPriceIds =
        [
            .. order.Lines
                .Select(static line => line.ProviderPriceId)
                .Order(StringComparer.Ordinal),
        ];
        string[] actualPriceIds =
        [
            .. items
                .Select(static item => item.Price?.Id ?? string.Empty)
                .Order(StringComparer.Ordinal),
        ];

        return !expectedPriceIds.SequenceEqual(actualPriceIds, StringComparer.Ordinal) ||
               items.Exists(static item => item.Quantity != 1)
            ? BillingFailureCodes.PriceMismatch
            : null;
    }

    private static ClientSettings? ReadClientSettings(StripeCommerceOptions options, out string? failure)
    {
        failure = BillingFailureCodes.InvalidConfiguration;
        if (!TryParseMode(options.Mode, out var mode))
        {
            return null;
        }

        var apiKey = NormalizeOptional(options.SecretKey);
        if (apiKey is null || !KeyMatchesMode(apiKey, mode))
        {
            return null;
        }

        if (!TryNormalizeApiBase(options.ApiBaseUrl, mode, out var apiBase))
        {
            return null;
        }

        failure = null;
        return new ClientSettings(apiKey, apiBase);
    }

    private static bool TryValidateOrder(BillingOrder order, out string failureCode)
    {
        var valid = !string.IsNullOrWhiteSpace(order.CatalogKey) &&
                    order.CatalogKey.Length <= 200 &&
                    TryExpectedCurrency(order.Lines, out _) &&
                    TryExpectedTotal(order.Lines, out _);
        failureCode = valid ? string.Empty : BillingFailureCodes.InvalidConfiguration;
        return valid;
    }

    private bool TryResolveExpectedLines(
        BillingPrice price,
        out List<(BillingPriceLine Line, string ProviderPriceId)> expectedLines,
        out string expectedCurrency)
    {
        expectedLines = [];
        expectedCurrency = string.Empty;
        if (!TryExpectedPriceCurrency(price.Lines, out expectedCurrency))
        {
            return false;
        }

        foreach (var line in price.Lines)
        {
            if (!_options.PriceIds.TryGetValue(line.Code, out var configuredId) ||
                string.IsNullOrWhiteSpace(configuredId))
            {
                return false;
            }

            expectedLines.Add((line, configuredId.Trim()));
        }

        return true;
    }

    private async Task<BillingPriceVerification> VerifyPricesAsync(
        List<(BillingPriceLine Line, string ProviderPriceId)> expectedLines,
        string expectedCurrency,
        BillingProviderMode mode,
        CancellationToken cancellationToken)
    {
        List<BillingVerifiedPriceLine> verified = [];
        foreach (var (line, providerPriceId) in expectedLines)
        {
            var providerPrice = await _prices!
                .GetAsync(providerPriceId, options: null, requestOptions: null, cancellationToken)
                .ConfigureAwait(false);
            if (!IsExpectedPrice(providerPrice, line, providerPriceId, expectedCurrency, mode))
            {
                return PriceFailure(BillingFailureCodes.PriceMismatch);
            }

            verified.Add(new BillingVerifiedPriceLine(
                line.Code,
                providerPrice.Id,
                providerPrice.UnitAmount!.Value,
                NormalizeCurrency(providerPrice.Currency)!,
                providerPrice.Recurring!.Interval,
                checked((int)providerPrice.Recurring.IntervalCount)));
        }

        return new BillingPriceVerification(
            BillingPriceVerificationStatus.Verified,
            mode,
            _accountId,
            verified,
            FailureCode: null);
    }

    private async Task<BillingCheckout> CreateOrRecoverAsync(
        BillingOrder order,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (!TryValidateOrder(order, out _))
        {
            return CheckoutFailure(BillingCheckoutStatus.Unavailable, BillingFailureCodes.InvalidConfiguration);
        }

        if (_mode is not { } mode ||
            _configurationFailure is not null ||
            _checkoutConfigurationFailure is not null ||
            _sessions is null ||
            _subscriptions is null)
        {
            return CheckoutFailure(BillingCheckoutStatus.Unavailable, BillingFailureCodes.InvalidConfiguration);
        }

        var now = _timeProvider.GetUtcNow();
        var requestedAt = order.RequestedAt == default ? now : order.RequestedAt;
        var withinRetention = now - requestedAt <= IdempotencyRetention;

        return order.CheckoutSessionId is not null || order.ProviderSubscriptionId is not null
            ? await RecoverBoundAsync(order, mode, withinRetention, cancellationToken).ConfigureAwait(false)
            : await CreateOrSearchAsync(order, mode, withinRetention, cancellationToken).ConfigureAwait(false);
    }

    private Task<BillingCheckout> CreateOrSearchAsync(
        BillingOrder order,
        BillingProviderMode mode,
        bool withinRetention,
        CancellationToken cancellationToken)
        => withinRetention
            ? CreateCoreAsync(order, mode, cancellationToken)
            : SearchAndAdoptAsync(order, mode, cancellationToken);

    private async Task<BillingCheckout> CreateCoreAsync(
        BillingOrder order,
        BillingProviderMode mode,
        CancellationToken cancellationToken)
    {
        var options = BuildSessionOptions(order);
        var requestOptions = new RequestOptions { IdempotencyKey = BuildIdempotencyKey(order.OrderId) };
        var attempt = await CreateAttemptAsync(options, requestOptions, cancellationToken).ConfigureAwait(false);
        if (attempt.Session is not null)
        {
            return ClassifySession(attempt.Session, order, mode);
        }

        if (!attempt.Retryable)
        {
            return CheckoutFailure(BillingCheckoutStatus.Unavailable, mode, attempt.FailureCode);
        }

        attempt = await CreateAttemptAsync(options, requestOptions, cancellationToken).ConfigureAwait(false);
        var status = attempt.Retryable ? BillingCheckoutStatus.Pending : BillingCheckoutStatus.Unavailable;
        return attempt.Session is not null
            ? ClassifySession(attempt.Session, order, mode)
            : CheckoutFailure(status, mode, attempt.FailureCode);
    }

    private async Task<CreateAttempt> CreateAttemptAsync(
        SessionCreateOptions options,
        RequestOptions requestOptions,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await _sessions!
                .CreateAsync(options, requestOptions, cancellationToken)
                .ConfigureAwait(false);
            return new CreateAttempt(session, FailureCode: null, Retryable: false);
        }
        catch (StripeException ex)
        {
            return new CreateAttempt(
                Session: null,
                FailureCode: MapProviderFailure(ex),
                Retryable: IsAmbiguous(ex));
        }
        catch (HttpRequestException)
        {
            return new CreateAttempt(Session: null, FailureCode: BillingFailureCodes.ProviderUnavailable, Retryable: true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new CreateAttempt(Session: null, FailureCode: BillingFailureCodes.ProviderUnavailable, Retryable: true);
        }
    }

    private async Task<BillingCheckout> RecoverBoundAsync(
        BillingOrder order,
        BillingProviderMode mode,
        bool withinRetention,
        CancellationToken cancellationToken)
    {
        var boundSession = await GetSessionOrNullAsync(order.CheckoutSessionId, cancellationToken).ConfigureAwait(false);
        if (boundSession is not null)
        {
            return ClassifySession(boundSession, order, mode);
        }

        if (order.ProviderSubscriptionId is not null)
        {
            var attempt = await ListSessionsAsync(order.ProviderSubscriptionId, cancellationToken)
                .ConfigureAwait(false);
            if (attempt.Sessions is null)
            {
                return CheckoutFailure(
                    attempt.Retryable ? BillingCheckoutStatus.Pending : BillingCheckoutStatus.Unavailable,
                    mode,
                    attempt.FailureCode);
            }

            var session = attempt.Sessions.Data?.FirstOrDefault();
            return session is null
                ? CheckoutFailure(BillingCheckoutStatus.Unavailable, mode, BillingFailureCodes.CheckoutUnavailable)
                : ClassifySession(session, order, mode);
        }

        return withinRetention
            ? await CreateCoreAsync(order, mode, cancellationToken).ConfigureAwait(false)
            : await SearchAndAdoptAsync(order, mode, cancellationToken).ConfigureAwait(false);
    }

    private async Task<BillingCheckout> SearchAndAdoptAsync(
        BillingOrder order,
        BillingProviderMode mode,
        CancellationToken cancellationToken)
    {
        var search = await SearchSubscriptionsAsync(order, cancellationToken).ConfigureAwait(false);
        if (search.Results is null)
        {
            return CheckoutFailure(
                search.Retryable ? BillingCheckoutStatus.Pending : BillingCheckoutStatus.Unavailable,
                mode,
                search.FailureCode);
        }

        if (search.Results.HasMore)
        {
            return CheckoutFailure(BillingCheckoutStatus.Unavailable, mode, BillingFailureCodes.CheckoutUnavailable);
        }

        var expectedCurrency = NormalizeCurrency(order.Lines[0].Currency);
        List<Subscription> matches =
        [
            .. search.Results.Where(candidate =>
                expectedCurrency is not null &&
                VerifySubscriptionBinding(candidate, order, mode, expectedCurrency) is null),
        ];

        if (matches.Count != 1)
        {
            return CheckoutFailure(BillingCheckoutStatus.Unavailable, mode, BillingFailureCodes.CheckoutUnavailable);
        }

        var sessions = await ListSessionsAsync(matches[0].Id, cancellationToken).ConfigureAwait(false);
        if (sessions.Sessions is null)
        {
            return CheckoutFailure(
                sessions.Retryable ? BillingCheckoutStatus.Pending : BillingCheckoutStatus.Unavailable,
                mode,
                sessions.FailureCode);
        }

        var session = sessions.Sessions.Data?.FirstOrDefault();
        return session is null
            ? CheckoutFailure(BillingCheckoutStatus.Unavailable, mode, BillingFailureCodes.CheckoutUnavailable)
            : ClassifySession(session, order, mode);
    }

    private async Task<SearchAttempt> SearchSubscriptionsAsync(
        BillingOrder order,
        CancellationToken cancellationToken)
    {
        try
        {
            var results = await _subscriptions!.SearchAsync(
                    new SubscriptionSearchOptions
                    {
                        Query = $"metadata['{MetadataOrderId}']:'{order.OrderId:D}'",
                        Limit = 3,
                    },
                    requestOptions: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return SearchAttempt.Success(results);
        }
        catch (StripeException ex)
        {
            return SearchAttempt.Failure(MapProviderFailure(ex), retryable: IsAmbiguous(ex));
        }
        catch (HttpRequestException)
        {
            return SearchAttempt.Failure(BillingFailureCodes.ProviderUnavailable, retryable: true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SearchAttempt.Failure(BillingFailureCodes.ProviderUnavailable, retryable: true);
        }
    }

    private BillingCheckout ClassifySession(Session session, BillingOrder order, BillingProviderMode mode)
    {
        var bindingFailure = VerifySessionBinding(session, order, mode);
        if (bindingFailure is not null)
        {
            return CheckoutFailure(BillingCheckoutStatus.Unavailable, mode, bindingFailure, session.Id);
        }

        if (string.Equals(session.Status, "complete", StringComparison.Ordinal))
        {
            return CheckoutFailure(BillingCheckoutStatus.Pending, mode, BillingFailureCodes.CheckoutUnavailable, session.Id);
        }

        var usable = string.Equals(session.Status, "open", StringComparison.Ordinal) &&
                     _checkoutOrigin is not null &&
                     !string.IsNullOrWhiteSpace(session.Url) &&
                     IsAllowedCheckoutUrl(session.Url, _checkoutOrigin);
        DateTimeOffset? expiresAt = session.ExpiresAt == default ? null : AsUtc(session.ExpiresAt);
        return usable
            ? new BillingCheckout(
                BillingCheckoutStatus.Created,
                session.Id,
                session.Url,
                mode,
                _accountId,
                expiresAt,
                FailureCode: null)
            : CheckoutFailure(BillingCheckoutStatus.Unavailable, mode, BillingFailureCodes.CheckoutUnavailable, session.Id);
    }

    private async Task<Session?> GetSessionOrNullAsync(string? sessionId, CancellationToken cancellationToken)
    {
        if (sessionId is null)
        {
            return null;
        }

        try
        {
            return await _sessions!.GetAsync(sessionId, options: null, requestOptions: null, cancellationToken).ConfigureAwait(false);
        }
        catch (StripeException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task<ListAttempt> ListSessionsAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var sessions = await _sessions!.ListAsync(
                    new SessionListOptions { Subscription = subscriptionId, Limit = 1 },
                    requestOptions: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return ListAttempt.Success(sessions);
        }
        catch (StripeException ex)
        {
            return ListAttempt.Failure(
                ex.HttpStatusCode == HttpStatusCode.NotFound
                    ? BillingFailureCodes.CheckoutUnavailable
                    : MapProviderFailure(ex),
                retryable: IsAmbiguous(ex));
        }
        catch (HttpRequestException)
        {
            return ListAttempt.Failure(BillingFailureCodes.ProviderUnavailable, retryable: true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ListAttempt.Failure(BillingFailureCodes.ProviderUnavailable, retryable: true);
        }
    }

    private async Task<Subscription?> GetSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _subscriptions!
                .GetAsync(subscriptionId, options: null, requestOptions: null, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (StripeException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task<Invoice?> GetInvoiceAsync(string invoiceId, CancellationToken cancellationToken)
    {
        try
        {
            return await _invoices!
                .GetAsync(invoiceId, options: null, requestOptions: null, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (StripeException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task<Charge?> GetChargeAsync(string chargeId, CancellationToken cancellationToken)
    {
        try
        {
            return await _charges!
                .GetAsync(chargeId, options: null, requestOptions: null, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (StripeException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task<bool?> HasActiveDisputeAsync(string chargeId, CancellationToken cancellationToken)
    {
        try
        {
            var disputes = await _disputes!.ListAsync(
                    new DisputeListOptions { Charge = chargeId, Limit = 10 },
                    requestOptions: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return disputes.Data?.Exists(static dispute => ActiveDisputeStatuses.Contains(dispute.Status)) == true;
        }
        catch (StripeException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private SessionCreateOptions BuildSessionOptions(BillingOrder order)
    {
        var metadata = BuildMetadata(order);
        return new SessionCreateOptions
        {
            Mode = SubscriptionMode,
            ClientReferenceId = order.OrderId.ToString("D"),
            SuccessUrl = ExpandReturnUrl(_successUrlTemplate!, order.OrderId),
            CancelUrl = ExpandReturnUrl(_cancelUrlTemplate!, order.OrderId),
            AllowPromotionCodes = false,
            AutomaticTax = new SessionAutomaticTaxOptions { Enabled = false },
            Metadata = metadata,
            LineItems = [.. order.Lines.Select(static line => new SessionLineItemOptions { Price = line.ProviderPriceId, Quantity = 1 })],
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>(metadata, StringComparer.Ordinal),
            },
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                Metadata = new Dictionary<string, string>(metadata, StringComparer.Ordinal),
            },
            InvoiceCreation = new SessionInvoiceCreationOptions
            {
                Enabled = true,
                InvoiceData = new SessionInvoiceCreationInvoiceDataOptions
                {
                    Metadata = new Dictionary<string, string>(metadata, StringComparer.Ordinal),
                },
            },
        };
    }

    private BillingCheckout CheckoutFailure(
        BillingCheckoutStatus status,
        string? failureCode,
        string? sessionId = null)
        => CheckoutFailure(status, _mode ?? BillingProviderMode.Test, failureCode, sessionId);

    private BillingCheckout CheckoutFailure(
        BillingCheckoutStatus status,
        BillingProviderMode mode,
        string? failureCode,
        string? sessionId = null)
        => new(
            Status: status,
            ProviderSessionId: sessionId,
            CheckoutUrl: null,
            Mode: mode,
            AccountId: _accountId,
            ExpiresAt: null,
            FailureCode: failureCode);

    private BillingPriceVerification PriceFailure(string failureCode)
        => new(
            BillingPriceVerificationStatus.Unavailable,
            _mode ?? BillingProviderMode.Test,
            _accountId,
            [],
            failureCode);

    private ReconcileState? CreateState(BillingOrder order, DateTimeOffset now)
    {
        var currency = string.Empty;
        var total = 0L;
        var mode = _mode;
        var unavailable = mode is null ||
                          _configurationFailure is not null ||
                          _sessions is null ||
                          _subscriptions is null ||
                          _invoices is null ||
                          _charges is null ||
                          _disputes is null ||
                          !TryExpectedCurrency(order.Lines, out currency) ||
                          !TryExpectedTotal(order.Lines, out total);
        return unavailable ? null : new ReconcileState(order, mode.GetValueOrDefault(), now, currency, total);
    }

    private async Task<BillingReconciliation?> LoadSessionAsync(
        ReconcileState state,
        CancellationToken cancellationToken)
    {
        if (state.Order.CheckoutSessionId is null)
        {
            state.SubscriptionId = state.Order.ProviderSubscriptionId;
            return null;
        }

        var session = await GetSessionOrNullAsync(state.Order.CheckoutSessionId, cancellationToken)
            .ConfigureAwait(false);
        if (session is null)
        {
            return Reconciliation(state, BillingEvidenceStatus.Unavailable, BillingFailureCodes.ProviderUnavailable, sessionId: state.Order.CheckoutSessionId);
        }

        var bindingFailure = VerifySessionBinding(session, state.Order, state.Mode);
        if (bindingFailure is not null)
        {
            return Reconciliation(state, BillingEvidenceStatus.Mismatch, bindingFailure, sessionId: session.Id);
        }

        state.SessionId = session.Id;
        state.SubscriptionId = state.Order.ProviderSubscriptionId ?? session.SubscriptionId;
        return !string.IsNullOrWhiteSpace(session.SubscriptionId) &&
               !string.IsNullOrWhiteSpace(state.Order.ProviderSubscriptionId) &&
               !string.Equals(session.SubscriptionId, state.Order.ProviderSubscriptionId, StringComparison.Ordinal)
            ? Reconciliation(state, BillingEvidenceStatus.Mismatch, BillingFailureCodes.BindingMismatch, sessionId: session.Id)
            : null;
    }

    private async Task<BillingReconciliation?> LoadSubscriptionAsync(
        ReconcileState state,
        CancellationToken cancellationToken)
    {
        var subscription = await GetSubscriptionAsync(state.SubscriptionId!, cancellationToken).ConfigureAwait(false);
        if (subscription is null)
        {
            return Reconciliation(state, BillingEvidenceStatus.Unavailable, BillingFailureCodes.ProviderUnavailable, subscriptionId: state.SubscriptionId);
        }

        var bindingFailure = VerifySubscriptionBinding(subscription, state.Order, state.Mode, state.Currency);
        if (bindingFailure is not null)
        {
            return Reconciliation(state, BillingEvidenceStatus.Mismatch, bindingFailure, subscriptionId: subscription.Id);
        }

        state.SubscriptionId = subscription.Id;
        if (string.Equals(subscription.Status, "canceled", StringComparison.Ordinal) ||
            string.Equals(subscription.Status, "incomplete_expired", StringComparison.Ordinal))
        {
            return Reconciliation(state, BillingEvidenceStatus.Canceled, failureCode: null, subscriptionId: subscription.Id);
        }

        state.InvoiceId = subscription.LatestInvoiceId ?? subscription.LatestInvoice?.Id;
        return string.IsNullOrWhiteSpace(state.InvoiceId) ||
               string.Equals(subscription.Status, "incomplete", StringComparison.Ordinal)
            ? Reconciliation(state, BillingEvidenceStatus.Pending, failureCode: null, subscriptionId: subscription.Id)
            : null;
    }

    private async Task<BillingReconciliation?> LoadInvoiceAsync(
        ReconcileState state,
        CancellationToken cancellationToken)
    {
        var invoice = await GetInvoiceAsync(state.InvoiceId!, cancellationToken).ConfigureAwait(false);
        if (invoice is null)
        {
            return Reconciliation(state, BillingEvidenceStatus.Unavailable, BillingFailureCodes.ProviderUnavailable, invoiceId: state.InvoiceId);
        }

        if (invoice.Livemode != IsLive(state.Mode) ||
            (invoice.Parent?.SubscriptionDetails?.SubscriptionId is string invoiceSubscriptionId &&
             !string.Equals(invoiceSubscriptionId, state.SubscriptionId, StringComparison.Ordinal)))
        {
            return Reconciliation(state, BillingEvidenceStatus.Mismatch, BillingFailureCodes.BindingMismatch, invoiceId: invoice.Id);
        }

        if (!string.Equals(NormalizeCurrency(invoice.Currency), state.Currency, StringComparison.Ordinal))
        {
            return Reconciliation(state, BillingEvidenceStatus.Mismatch, BillingFailureCodes.CurrencyMismatch, invoiceId: invoice.Id);
        }

        state.Invoice = invoice;
        state.InvoiceId = invoice.Id;
        if (!string.Equals(invoice.Status, "paid", StringComparison.Ordinal) ||
            invoice.AmountRemaining != 0 ||
            invoice.AmountPaid <= 0)
        {
            return Reconciliation(state, BillingEvidenceStatus.Pending, failureCode: null, invoiceId: invoice.Id);
        }

        if (invoice.AmountPaid != state.ExpectedTotal)
        {
            return Reconciliation(state, BillingEvidenceStatus.Mismatch, BillingFailureCodes.AmountMismatch, invoiceId: invoice.Id);
        }

        state.PeriodStart = AsUtc(invoice.PeriodStart);
        state.PeriodEnd = AsUtc(invoice.PeriodEnd);
        state.AmountPaid = invoice.AmountPaid;
        return state.PeriodEnd <= state.PeriodStart
            ? Reconciliation(state, BillingEvidenceStatus.Unavailable, BillingFailureCodes.ProviderUnavailable, invoiceId: invoice.Id)
            : null;
    }

    private async Task<BillingReconciliation?> LoadChargeAsync(
        ReconcileState state,
        CancellationToken cancellationToken)
    {
        var invoice = state.Invoice!;
        var chargeId = FindSingleChargeId(invoice);
        if (chargeId is null)
        {
            return Reconciliation(state, BillingEvidenceStatus.Unavailable, BillingFailureCodes.ProviderUnavailable);
        }

        var charge = await GetChargeAsync(chargeId, cancellationToken).ConfigureAwait(false);
        if (charge is null)
        {
            return Reconciliation(state, BillingEvidenceStatus.Unavailable, BillingFailureCodes.ProviderUnavailable, chargeId: chargeId);
        }

        state.Charge = charge;
        state.ChargeId = charge.Id;
        state.PaymentIntentId = charge.PaymentIntentId;
        if (charge.Livemode != IsLive(state.Mode) || !charge.Paid)
        {
            return Reconciliation(state, BillingEvidenceStatus.Mismatch, BillingFailureCodes.BindingMismatch, chargeId: charge.Id);
        }

        if (!string.Equals(NormalizeCurrency(charge.Currency), state.Currency, StringComparison.Ordinal))
        {
            return Reconciliation(state, BillingEvidenceStatus.Mismatch, BillingFailureCodes.CurrencyMismatch, chargeId: charge.Id);
        }

        var amountMismatch = charge.Amount != invoice.AmountPaid
            ? Reconciliation(state, BillingEvidenceStatus.Mismatch, BillingFailureCodes.AmountMismatch, chargeId: charge.Id)
            : null;
        return amountMismatch ?? (charge.AmountRefunded > 0 || charge.Refunded
            ? Reconciliation(state, BillingEvidenceStatus.Refunded, failureCode: null, chargeId: charge.Id, paymentIntentId: charge.PaymentIntentId)
            : null);
    }

    private async Task<BillingReconciliation> EvaluateAsync(
        ReconcileState state,
        CancellationToken cancellationToken)
    {
        var dispute = state.Charge!.Disputed
            ? await HasActiveDisputeAsync(state.ChargeId!, cancellationToken).ConfigureAwait(false)
            : false;
        var unavailable = dispute is null
            ? Reconciliation(state, BillingEvidenceStatus.Unavailable, BillingFailureCodes.ProviderUnavailable)
            : null;
        var status = state.PeriodEnd > state.Now ? BillingEvidenceStatus.Paid : BillingEvidenceStatus.Pending;
        return unavailable ?? (dispute == true
            ? Reconciliation(state, BillingEvidenceStatus.Disputed, failureCode: null, chargeId: state.ChargeId, paymentIntentId: state.PaymentIntentId)
            : Reconciliation(state, status, failureCode: null, chargeId: state.ChargeId, paymentIntentId: state.PaymentIntentId));
    }

    private BillingReconciliation Reconciliation(
        DateTimeOffset now,
        BillingEvidenceStatus status,
        BillingProviderMode mode,
        string? failureCode,
        string? sessionId = null,
        string? subscriptionId = null,
        string? invoiceId = null,
        string? chargeId = null,
        string? paymentIntentId = null,
        DateTimeOffset? periodStart = null,
        DateTimeOffset? periodEnd = null,
        long? amountPaid = null,
        string? currency = null)
        => new(
            status,
            mode,
            _accountId,
            sessionId,
            subscriptionId,
            invoiceId,
            chargeId,
            paymentIntentId,
            periodStart,
            periodEnd,
            amountPaid,
            currency,
            now,
            failureCode);

    private BillingReconciliation Reconciliation(
        ReconcileState state,
        BillingEvidenceStatus status,
        string? failureCode,
        string? sessionId = null,
        string? subscriptionId = null,
        string? invoiceId = null,
        string? chargeId = null,
        string? paymentIntentId = null)
    {
        var includePeriod = status is BillingEvidenceStatus.Paid
            or BillingEvidenceStatus.Refunded
            or BillingEvidenceStatus.Disputed
            or BillingEvidenceStatus.Pending;
        return Reconciliation(
            state.Now,
            status,
            state.Mode,
            failureCode,
            sessionId ?? state.SessionId,
            subscriptionId ?? state.SubscriptionId,
            invoiceId ?? state.InvoiceId,
            chargeId ?? state.ChargeId,
            paymentIntentId ?? state.PaymentIntentId,
            includePeriod ? state.PeriodStart : null,
            includePeriod ? state.PeriodEnd : null,
            includePeriod ? state.AmountPaid : null,
            state.Currency);
    }

    private Event? VerifyWebhookEvent(string rawBody, string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(rawBody) || string.IsNullOrWhiteSpace(signatureHeader))
        {
            return null;
        }

        var webhookSecret = NormalizeOptional(_options.WebhookSecret);
        if (_mode is not { } mode || _accountId is null || webhookSecret is null)
        {
            return null;
        }

        Event providerEvent;
        try
        {
            providerEvent = EventUtility.ConstructEvent(
                rawBody,
                signatureHeader,
                webhookSecret,
                _options.WebhookToleranceSeconds,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(providerEvent.Id) ||
               providerEvent.Livemode != IsLive(mode) ||
               (!string.IsNullOrWhiteSpace(providerEvent.Account) &&
                !string.Equals(providerEvent.Account, _accountId, StringComparison.Ordinal))
            ? null
            : providerEvent;
    }

    private BillingNotice MapNotice(Event providerEvent)
    {
        Guid? orderId = null;
        string? sessionId = null;
        string? subscriptionId = null;
        string? invoiceId = null;
        string? chargeId = null;
        string? paymentIntentId = null;
        string? disputeId = null;

        var target = providerEvent.Data?.Object;
        if (target is Session session)
        {
            sessionId = session.Id;
            subscriptionId = session.SubscriptionId;
            orderId = ParseOrderId(session.Metadata) ?? ParseOrderIdFromText(session.ClientReferenceId);
        }
        else if (target is Invoice invoice)
        {
            invoiceId = invoice.Id;
            subscriptionId = invoice.Parent?.SubscriptionDetails?.SubscriptionId;
            orderId = ParseOrderId(invoice.Parent?.SubscriptionDetails?.Metadata) ?? ParseOrderId(invoice.Metadata);
        }
        else if (target is Subscription subscription)
        {
            subscriptionId = subscription.Id;
            orderId = ParseOrderId(subscription.Metadata);
        }
        else if (target is Charge charge)
        {
            chargeId = charge.Id;
            paymentIntentId = charge.PaymentIntentId;
            orderId = ParseOrderId(charge.Metadata);
        }
        else if (target is Refund refund)
        {
            chargeId = refund.ChargeId;
            paymentIntentId = refund.PaymentIntentId;
            orderId = ParseOrderId(refund.Metadata);
        }
        else if (target is Dispute dispute)
        {
            disputeId = dispute.Id;
            chargeId = dispute.ChargeId;
            paymentIntentId = dispute.PaymentIntentId;
            orderId = ParseOrderId(dispute.Metadata);
        }

        return new BillingNotice(
            providerEvent.Id,
            providerEvent.Type,
            AsUtc(providerEvent.Created),
            _mode!.Value,
            _accountId,
            orderId,
            sessionId,
            subscriptionId,
            invoiceId,
            chargeId,
            paymentIntentId,
            disputeId);
    }

    private sealed record ClientSettings(string ApiKey, string ApiBase);

    private sealed record ListAttempt(StripeList<Session>? Sessions, string? FailureCode, bool Retryable)
    {
        public static ListAttempt Success(StripeList<Session> sessions)
            => new(sessions, FailureCode: null, Retryable: false);

        public static ListAttempt Failure(string failureCode, bool retryable)
            => new(Sessions: null, FailureCode: failureCode, Retryable: retryable);
    }

    private sealed record SearchAttempt(StripeSearchResult<Subscription>? Results, string? FailureCode, bool Retryable)
    {
        public static SearchAttempt Success(StripeSearchResult<Subscription> results)
            => new(results, FailureCode: null, Retryable: false);

        public static SearchAttempt Failure(string failureCode, bool retryable)
            => new(Results: null, FailureCode: failureCode, Retryable: retryable);
    }

    private sealed record CreateAttempt(Session? Session, string? FailureCode, bool Retryable);

    private sealed class ReconcileState(
        BillingOrder order,
        BillingProviderMode mode,
        DateTimeOffset now,
        string currency,
        long expectedTotal)
    {
        public BillingOrder Order { get; } = order;

        public BillingProviderMode Mode { get; } = mode;

        public DateTimeOffset Now { get; } = now;

        public string Currency { get; } = currency;

        public long ExpectedTotal { get; } = expectedTotal;

        public Invoice? Invoice { get; set; }

        public Charge? Charge { get; set; }

        public string? SessionId { get; set; }

        public string? SubscriptionId { get; set; }

        public string? InvoiceId { get; set; }

        public string? ChargeId { get; set; }

        public string? PaymentIntentId { get; set; }

        public DateTimeOffset? PeriodStart { get; set; }

        public DateTimeOffset? PeriodEnd { get; set; }

        public long? AmountPaid { get; set; }
    }
}
