using SharedKernel.Core.CQRS;

namespace SharedKernel.ArchitectureTests;

internal sealed class MutableCommand : ICommand<int>
{
    public int Value { get; set; }
}
