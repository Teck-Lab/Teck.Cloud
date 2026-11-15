using SharedKernel.Core.CQRS;

namespace SharedKernel.ArchitectureTests;

internal sealed class MutableQuery : IQuery<int>
{
    public int Value { get; set; }
}
