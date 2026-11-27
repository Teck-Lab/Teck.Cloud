namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>A customer access token with its canonical account.</summary>
/// <param name="AccessToken">The bearer token; never log it.</param>
/// <param name="Account">The authenticated account.</param>
public sealed record CustomerToken(string AccessToken, CustomerAccount Account);
