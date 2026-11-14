namespace SharedKernel.Core.MultiTenancy;

/// <summary>Holds validated identity for one dependency-injection scope; never a singleton.</summary>
public sealed class OperationContext
{
    private DataScope? _scope;

    /// <summary>Gets the scope; access before initialization fails.</summary>
    /// <exception cref="InvalidOperationException">The operation has not been initialized.</exception>
    public DataScope Scope => _scope ?? throw new InvalidOperationException("An explicit data scope is required.");

    /// <summary>Gets the authenticated actor identifier, if applicable.</summary>
    public string? ActorId { get; private set; }

    /// <summary>Establishes scope once, before resolving data adapters.</summary>
    /// <param name="scope">The authorized tenant or explicit global scope.</param>
    /// <param name="actorId">The authenticated actor.</param>
    /// <exception cref="InvalidOperationException">A different scope or actor is already established.</exception>
    public void Initialize(DataScope scope, string? actorId = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (_scope is not null && (_scope != scope || !string.Equals(ActorId, actorId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("An operation cannot change its established identity.");
        }

        _scope = scope;
        ActorId = actorId;
    }
}
