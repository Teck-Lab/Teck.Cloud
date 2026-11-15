using SharedKernel.Core.CQRS;

namespace SharedKernel.ArchitectureTests;

internal sealed record GoodCommand : ICommand<int>;
