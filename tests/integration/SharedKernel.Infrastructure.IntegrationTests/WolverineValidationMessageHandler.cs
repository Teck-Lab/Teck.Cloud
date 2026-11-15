namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Handles valid validation messages and records whether dispatch reached the handler.</summary>
public static class WolverineValidationMessageHandler
{
    /// <summary>Records a successfully validated message.</summary>
    /// <param name="message">The validated message.</param>
    /// <param name="recorder">The host-local invocation recorder.</param>
    /// <returns>A completed handler task.</returns>
    public static Task HandleAsync(WolverineValidationMessage message, WolverineValidationRecorder recorder)
    {
        _ = message;
        recorder.Record();
        return Task.CompletedTask;
    }
}
