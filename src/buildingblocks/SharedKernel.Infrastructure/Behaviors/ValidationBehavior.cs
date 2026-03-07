using ErrorOr;
using FluentValidation;
using FluentValidation.Results;
using Mediator;

namespace SharedKernel.Infrastructure.Behaviors
{
    /// <summary>
    /// A pipeline behavior that executes FluentValidation validators for requests
    /// returning ErrorOr-compatible responses.
    /// </summary>
    /// <typeparam name="TRequest">The incoming request type.</typeparam>
    /// <typeparam name="TResponse">The response type implementing <see cref="IErrorOr"/>.</typeparam>
    public sealed class ValidationBehavior<TRequest, TResponse>(
        IEnumerable<IValidator<TRequest>> validators)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : IErrorOr
    {
        /// <summary>
        /// Executes all registered validators for the request and short-circuits the pipeline
        /// when validation fails, returning validation errors as ErrorOr.
        /// </summary>
        /// <param name="message">The request message being processed.</param>
        /// <param name="next">The delegate representing the next handler in the pipeline.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task representing the asynchronous operation, containing the response.</returns>
        public async ValueTask<TResponse> Handle(
            TRequest message,
            MessageHandlerDelegate<TRequest, TResponse> next,
            CancellationToken cancellationToken)
        {
            if (!validators.Any())
            {
                return await next(message, cancellationToken);
            }

            ValidationContext<TRequest> context = new(message);

            ValidationResult[] validationResults = await Task.WhenAll(
                validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

            List<Error> errors = validationResults
                .SelectMany(static (ValidationResult result) => result.Errors)
                .Where(failure => failure is not null)
                .Select(failure => Error.Validation(
                    code: failure.PropertyName,
                    description: failure.ErrorMessage))
                .Distinct()
                .ToList();

            if (errors.Count == 0)
            {
                return await next(message, cancellationToken);
            }

            return (dynamic)errors;
        }
    }
}
