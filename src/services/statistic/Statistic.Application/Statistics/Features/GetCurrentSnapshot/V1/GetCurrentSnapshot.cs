// <copyright file="GetCurrentSnapshot.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.CQRS;
using Statistic.Domain.Statistics;

namespace Statistic.Application.Statistics.Features.GetCurrentSnapshot.V1;

/// <summary>
/// Query that returns the latest statistics snapshot.
/// </summary>
public sealed record GetCurrentSnapshotQuery : IQuery<StatSnapshot>;

/// <summary>
/// Handler for <see cref="GetCurrentSnapshotQuery"/>.
/// </summary>
internal sealed class GetCurrentSnapshotQueryHandler(ISnapshotStore store)
    : IQueryHandler<GetCurrentSnapshotQuery, StatSnapshot>
{
    private readonly ISnapshotStore store = store;

    /// <inheritdoc/>
    public ValueTask<StatSnapshot> Handle(GetCurrentSnapshotQuery request, CancellationToken cancellationToken)
        => ValueTask.FromResult(this.store.Current);
}
