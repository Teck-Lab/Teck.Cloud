using System.Text.Json.Serialization.Metadata;
using ErrorOr;
using Microsoft.AspNetCore.Http;

namespace SharedKernel.Infrastructure.Http;

/// <summary>Maps handler results to ordinary HTTP results without a custom endpoint base class.</summary>
public static class ErrorOrHttp
{
    /// <summary>Maps success using generated JSON metadata, or errors to standard problem details.</summary>
    /// <typeparam name="T">The response DTO.</typeparam>
    /// <param name="result">The handler result.</param>
    /// <param name="json">Generated response metadata.</param>
    /// <returns>A result usable with FastEndpoints Send.ResultAsync.</returns>
    public static IResult ToHttpResult<T>(this ErrorOr<T> result, JsonTypeInfo<T> json)
    {
        if (!result.IsError)
        {
            return TypedResults.Json(result.Value, json);
        }

        if (result.Errors.TrueForAll(error => error.Type == ErrorType.Validation))
        {
            return TypedResults.ValidationProblem(result.Errors.GroupBy(error => error.Code, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray(), StringComparer.Ordinal));
        }

        var status = result.FirstError.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Failure => StatusCodes.Status500InternalServerError,
            ErrorType.Unexpected => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError,
        };
        return TypedResults.Problem(detail: status == 500 ? null : result.FirstError.Description, statusCode: status, title: status == 500 ? "An unexpected error occurred." : result.FirstError.Code);
    }
}
