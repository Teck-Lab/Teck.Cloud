using SharedKernel.Core.CQRS;

namespace SharedKernel.ArchitectureTests;

internal sealed record GoodQuery : IQuery<int>;
