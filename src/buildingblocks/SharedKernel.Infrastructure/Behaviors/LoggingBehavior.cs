using System.Diagnostics;
using Mediator;
using Microsoft.Extensions.Logging;

namespace SharedKernel.Infrastructure.Behaviors
{
    /// <summary>
    /// A pipeline behavior that logs the start and end of message handling,
    /// including performance monitoring. This behavior is executed in the
    /// MediatR pipeline and is applied to every handled message.
    /// </summary>
    /// <typeparam name="TMessage">The type of the incoming message (request).</typeparam>
    /// <typeparam name="TResponse">The type of the response expected from the message handler.</typeparam>
    public sealed class LoggingBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
        where TMessage : IMessage
    {
        private readonly ILogger<LoggingBehavior<TMessage, TResponse>> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingBehavior{TMessage, TResponse}"/> class.
        /// </summary>
        /// <param name="logger">The logger used for writing log messages.</param>
        public LoggingBehavior(ILogger<LoggingBehavior<TMessage, TResponse>> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Handles the logging of request and response information, as well as performance tracking.
        /// Logs the start and completion of request processing. Emits a warning if the processing takes
        /// more than 3 seconds.
        /// </summary>
        /// <param name="message">The incoming request message.</param>
        /// <param name="next">The delegate representing the next handler in the pipeline.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation, containing the response.</returns>
        public async ValueTask<TResponse> Handle(
            TMessage message,
            MessageHandlerDelegate<TMessage, TResponse> next,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "[START] Handle request={Request} - Response={Response} - RequestData={RequestData}",
                typeof(TMessage).Name,
                typeof(TResponse).Name,
                message);

            Stopwatch timer = new();
            timer.Start();

            TResponse? response = await next(message, cancellationToken);

            timer.Stop();
            TimeSpan timeTaken = timer.Elapsed;
            if (timeTaken.TotalSeconds > 3)
            {
                _logger.LogWarning(
                    "[PERFORMANCE] The request {Request} took {TimeTaken} seconds.",
                    typeof(TMessage).Name,
                    timeTaken.TotalSeconds);
            }

            _logger.LogInformation(
                "[END] Handled {Request} with {Response} in {TimeTaken} seconds.",
                typeof(TMessage).Name,
                typeof(TResponse).Name,
                timeTaken.TotalSeconds);

            return response;
        }
    }
}
