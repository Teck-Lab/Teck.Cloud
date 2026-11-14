using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SharedKernel.Persistence.Database;

/// <summary>Checks scope at command execution, including cached queries and direct SQL.</summary>
internal sealed class RequiredScopeCommandInterceptor : DbCommandInterceptor
{
    /// <inheritdoc/>
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) => Check(eventData, result);

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Check(eventData, result));

    /// <inheritdoc/>
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result) => Check(eventData, result);

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Check(eventData, result));

    /// <inheritdoc/>
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result) => Check(eventData, result);

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Check(eventData, result));

    private static InterceptionResult<T> Check<T>(CommandEventData eventData, InterceptionResult<T> result)
    {
        if (eventData.Context is ScopedDbContext context)
        {
            _ = context.Scope;
        }

        return result;
    }
}
