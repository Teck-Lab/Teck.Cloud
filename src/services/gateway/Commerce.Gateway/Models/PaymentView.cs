namespace Teck.Cloud.Commerce.Gateway.Models;

/// <summary>Verified payment facts on the staff surface.</summary>
/// <param name="Status">Verified payment state.</param>
/// <param name="PaidThrough">Verified paid-period end, or null.</param>
public sealed record PaymentView(string Status, string? PaidThrough);
