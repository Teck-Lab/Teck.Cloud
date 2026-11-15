using ErrorOr;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Returns either a probe value or a typed validation failure.</summary>
public static class WolverineResultProbeHandler
{
    /// <summary>Handles a result probe.</summary>
    /// <param name="probe">The probe request.</param>
    /// <param name="state">The successful-result state recorder.</param>
    /// <returns>The value or validation failure.</returns>
    public static ErrorOr<Guid> Handle(WolverineResultProbe probe, WolverineResultProbeState state)
    {
        if (probe.ReturnError)
        {
            return Error.Validation("probe", "Requested result failure.");
        }

        state.RecordSuccess();
        return probe.Id;
    }
}
