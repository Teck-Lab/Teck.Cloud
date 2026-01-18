using System.Data;
using Mediator;
using Microsoft.Extensions.Logging;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;

namespace SharedKernel.Infrastructure.Behaviors
{
    /// <summary>
    /// A pipeline behavior that wraps the handling of a transactional command in a database transaction.
    /// Ensures that the command is executed within a single unit of work, committing only upon successful execution.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request, which must implement <see cref="ITransactionalCommand{TResponse}"/>.</typeparam>
    /// <typeparam name="TResponse">The type of the response returned by the handler.</typeparam>
    public sealed class TransactionalBehavior<TRequest, TResponse>(
        IUnitOfWork unitOfWork,
        ILogger<TransactionalBehavior<TRequest, TResponse>> logger)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : ITransactionalCommand<TResponse>
    {
        /// <summary>
        /// Handles the request within a database transaction. Begins a transaction, executes the next handler,
        /// commits the transaction if successful, and logs the transaction lifecycle.
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
            logger.LogInformation("Beginning transaction for {RequestName}", typeof(TRequest).Name);

            using IDbTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken: cancellationToken);
            TResponse response = await next(message, cancellationToken);
            transaction.Commit();

            logger.LogInformation("Committed transaction for {RequestName}", typeof(TRequest).Name);

            return response;
        }
    }
}
