namespace Customer.Api.Endpoints.V1.Tenants.CheckServiceReadiness;

/// <summary>
/// Response for service readiness check.
/// </summary>
/// <param name="IsReady">Whether the service is ready.</param>
internal record ServiceReadinessResponse(bool IsReady);
