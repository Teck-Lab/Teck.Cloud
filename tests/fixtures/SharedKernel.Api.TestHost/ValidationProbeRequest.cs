namespace SharedKernel.Api.TestHost;

/// <summary>The request DTO validated by the HTTP fixture.</summary>
public sealed class ValidationProbeRequest
{
    /// <summary>Gets or sets the name accepted by the endpoint.</summary>
    public string Name { get; set; } = string.Empty;
}
