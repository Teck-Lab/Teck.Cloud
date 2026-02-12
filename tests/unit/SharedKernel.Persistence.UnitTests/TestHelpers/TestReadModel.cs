using SharedKernel.Core.Domain;

namespace SharedKernel.Persistence.UnitTests.TestHelpers;

/// <summary>
/// Test read model for GenericReadRepository testing.
/// </summary>
internal sealed class TestReadModel : ReadModelBase<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Priority { get; set; }
    public string? Category { get; set; }
}
