using ErrorOr;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace SharedKernel.Infrastructure.Endpoints;

/// <summary>
/// Validates Minimal API arguments using registered FluentValidation validators
/// and returns a centralized ProblemDetails response when validation fails.
/// </summary>
public sealed class ValidationEndpointFilter : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var errors = new List<Error>();

        foreach (object? argument in context.Arguments)
        {
            if (argument is null || argument is HttpContext || argument is CancellationToken)
            {
                continue;
            }

            Type argumentType = argument.GetType();
            Type validatorType = typeof(IValidator<>).MakeGenericType(argumentType);
            object? validator = context.HttpContext.RequestServices.GetService(validatorType);

            if (validator is null)
            {
                continue;
            }

            ValidationResult validationResult = await ValidateArgumentAsync(validator, argument, context.HttpContext.RequestAborted);

            if (validationResult.IsValid)
            {
                continue;
            }

            errors.AddRange(validationResult.Errors
                .Where(failure => failure is not null)
                .Select(failure => Error.Validation(
                    code: failure.PropertyName,
                    description: failure.ErrorMessage)));
        }

        if (errors.Count > 0)
        {
            return errors.ToMinimalApiErrorResult(context.HttpContext);
        }

        return await next(context);
    }

    private static Task<ValidationResult> ValidateArgumentAsync(object validator, object argument, CancellationToken cancellationToken)
    {
        Type argumentType = argument.GetType();
        var method = typeof(ValidationEndpointFilter)
            .GetMethod(nameof(ValidateTypedAsync), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(argumentType);

        return (Task<ValidationResult>)method.Invoke(null, [validator, argument, cancellationToken])!;
    }

    private static Task<ValidationResult> ValidateTypedAsync<T>(object validator, object argument, CancellationToken cancellationToken)
    {
        IValidator<T> typedValidator = (IValidator<T>)validator;
        return typedValidator.ValidateAsync((T)argument, cancellationToken);
    }
}