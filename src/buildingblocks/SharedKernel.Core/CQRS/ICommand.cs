using Mediator;

namespace SharedKernel.Core.CQRS
{
    /// <summary>
    /// Command with no response.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "To disable warning")]
    public interface ICommand : ICommand<Unit>
    {
    }

    /// <summary>
    /// Command with response.
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "To disable warning")]
    public interface ICommand<out TResponse> : IRequest<TResponse>
    {
    }

    /// <summary>
    /// Transaction command with no response.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "To disable warning")]
    public interface ITransactionalCommand : ICommand<Unit>
    {
    }

    /// <summary>
    /// Transaction command with a sesponse.
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    public interface ITransactionalCommand<out TResponse> : ICommand<TResponse>
    {
    }
}
