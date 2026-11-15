namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Records successful result side effects in the no-container Wolverine test.</summary>
public sealed class WolverineResultProbeState
{
    private int _successfulCount;

    /// <summary>Gets the number of successful result side effects.</summary>
    public int SuccessfulCount => Volatile.Read(ref _successfulCount);

    /// <summary>Records one successful side effect.</summary>
    public void RecordSuccess() => _ = Interlocked.Increment(ref _successfulCount);
}
