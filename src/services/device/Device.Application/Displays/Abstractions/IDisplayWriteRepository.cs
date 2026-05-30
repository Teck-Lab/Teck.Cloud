// <copyright file="IDisplayWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DisplayAggregate;
using SharedKernel.Core.Database;

namespace Device.Application.Displays.Abstractions;

/// <summary>
/// Write repository for <see cref="Display"/> entities.
/// </summary>
public interface IDisplayWriteRepository : IGenericWriteRepository<Display, Guid>
{
    /// <summary>
    /// Checks whether a display with the given short serial already exists across all tenants.
    /// </summary>
    /// <param name="shortSerial">The short serial to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the serial is already registered; otherwise <see langword="false"/>.</returns>
    Task<bool> ExistsWithShortSerialGlobalAsync(string shortSerial, CancellationToken cancellationToken = default);
}
