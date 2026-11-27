using System.Text;
using SharedKernel.Infrastructure.MultiTenancy;
using Teck.Cloud.Commerce.Orders.Infrastructure.Work;

namespace Teck.Cloud.Commerce.Orders.Host.Composition;

/// <summary>The separately authenticated provider webhook ingress.</summary>
public static class WebhookEndpoints
{
    /// <summary>Maps the Stripe-compatible webhook on the HTTP/1 listener.</summary>
    /// <param name="app">The composed application.</param>
    public static void MapOrdersWebhook(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        _ = app.MapPost(
            "/internal/billing/stripe/v1/webhook",
            async (HttpContext http, StripeWebhookIntake intake, CancellationToken cancellationToken) =>
            {
                using var reader = new StreamReader(http.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                var signature = http.Request.Headers["Stripe-Signature"].ToString();
                var outcome = await intake.HandleAsync(body, signature, cancellationToken).ConfigureAwait(false);
                return outcome == WebhookIntakeOutcome.InvalidSignature
                    ? Results.BadRequest()
                    : Results.Ok();
            })
            .WithMetadata(OperationScope.Global)
            .AllowAnonymous()
            .ExcludeFromDescription();
    }
}
