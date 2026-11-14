using Xunit;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Serializes global diagnostics and console capture against other test collections.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TelemetryGroup
{
    /// <summary>Identifies tests that touch process-wide diagnostics.</summary>
    public const string Name = "Telemetry";

    private TelemetryGroup()
    {
    }
}
