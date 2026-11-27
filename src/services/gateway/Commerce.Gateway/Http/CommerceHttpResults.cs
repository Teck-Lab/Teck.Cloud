using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Http;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Gateway.Errors;
using Teck.Cloud.Commerce.Gateway.Models;
using Teck.Cloud.Commerce.Gateway.Serialization;

namespace Teck.Cloud.Commerce.Gateway.Http;

/// <summary>Writes only generated JSON and sanitized problem documents.</summary>
public static class CommerceHttpResults
{
    /// <summary>Writes a generated success projection.</summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="value">The projection.</param>
    /// <param name="json">Generated metadata for the projection.</param>
    /// <param name="statusCode">The contract status code.</param>
    /// <returns>The HTTP result.</returns>
    public static IResult Success<T>(T value, JsonTypeInfo<T> json, int statusCode = StatusCodes.Status200OK)
        where T : class =>
        TypedResults.Json(value, json, statusCode: statusCode);

    /// <summary>Writes the sanitized problem for a failed edge outcome.</summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="http">The current response.</param>
    /// <param name="result">The failed outcome.</param>
    /// <param name="json">Generated metadata for the projection.</param>
    /// <returns>The HTTP result.</returns>
    public static IResult Failure<T>(HttpContext http, GatewayResult<T> result, JsonTypeInfo<T> json)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(json);
        _ = json;
        return WriteProblem(http, result.Problem ?? CommerceProblemFactory.DependencyUnavailable());
    }

    /// <summary>Writes one sanitized problem with the frozen problem content type.</summary>
    /// <param name="http">The current response.</param>
    /// <param name="problem">The published problem.</param>
    /// <returns>The HTTP result.</returns>
    public static IResult WriteProblem(HttpContext http, CommerceProblemView problem)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(problem);
        if (problem.Status == StatusCodes.Status503ServiceUnavailable)
        {
            http.Response.Headers.RetryAfter = CommerceProblemFactory.RetryAfterSeconds(problem.Status).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return TypedResults.Json(problem, CommerceGatewayJsonContext.Default.CommerceProblemView, "application/problem+json", problem.Status);
    }

    /// <summary>Writes a generated 400 problem for request validation.</summary>
    /// <param name="http">The current response.</param>
    /// <returns>The HTTP result.</returns>
    public static IResult InvalidRequest(HttpContext http) =>
        WriteProblem(http, CommerceProblemFactory.FromCode(CommerceErrorCodes.InvalidRequest));
}
