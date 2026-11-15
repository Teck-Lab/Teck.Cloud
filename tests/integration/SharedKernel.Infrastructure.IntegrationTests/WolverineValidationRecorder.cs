namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Stores invocation evidence for the test host.</summary>
public sealed class WolverineValidationRecorder
{
    private int _calls;

    /// <summary>Gets the number of handler invocations.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <summary>Records one invocation.</summary>
    public void Record() => _ = Interlocked.Increment(ref _calls);
}
