using Mediator;

namespace SharedKernel.Core.CQRS
{
    /// <summary>
    /// Query interface with a response.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IQuery<out T> : IRequest<T>
        where T : notnull
    {
    }
}
