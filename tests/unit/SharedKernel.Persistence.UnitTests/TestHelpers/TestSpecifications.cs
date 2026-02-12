using Ardalis.Specification;

namespace SharedKernel.Persistence.UnitTests.TestHelpers;

/// <summary>
/// Specification to filter TestReadModel by name.
/// </summary>
internal sealed class TestByNameSpecification : Specification<TestReadModel>
{
    public TestByNameSpecification(string name)
    {
        Query.Where(e => e.Name == name);
    }
}

/// <summary>
/// Specification to filter TestReadModel by minimum priority.
/// </summary>
internal sealed class TestByPrioritySpecification : Specification<TestReadModel>
{
    public TestByPrioritySpecification(int minPriority)
    {
        Query.Where(e => e.Priority >= minPriority);
    }
}

/// <summary>
/// Specification to filter TestReadModel by category.
/// </summary>
internal sealed class TestByCategorySpecification : Specification<TestReadModel>
{
    public TestByCategorySpecification(string category)
    {
        Query.Where(e => e.Category == category);
    }
}

/// <summary>
/// Specification with projection to select only Name and Priority.
/// </summary>
internal sealed class TestNamePriorityProjectionSpecification : Specification<TestReadModel, TestNamePriorityDto>
{
    public TestNamePriorityProjectionSpecification()
    {
        Query.Select(e => new TestNamePriorityDto
        {
            Name = e.Name,
            Priority = e.Priority
        });
    }
}

/// <summary>
/// DTO for TestReadModel projection.
/// </summary>
internal sealed class TestNamePriorityDto
{
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
}
