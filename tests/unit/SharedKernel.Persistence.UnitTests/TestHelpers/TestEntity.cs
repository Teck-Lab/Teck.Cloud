using SharedKernel.Core.Domain;

namespace SharedKernel.Persistence.UnitTests.TestHelpers;

/// <summary>
/// Test entity for repository testing.
/// </summary>
internal sealed class TestEntity : BaseEntity, IAggregateRoot
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Priority { get; set; }
}
