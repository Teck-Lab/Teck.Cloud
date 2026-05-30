// <copyright file="IEslDeviceServer.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;

namespace Device.VendorWorker.Vendors;

/// <summary>
/// Vendor-routing port that dispatches a rendered image to the physical ESL device server.
/// Implementations are vendor-specific (Hanshow, SoluM, Stub, etc.). The worker hosts one instance
/// per supported vendor and routes messages by <see cref="Provider"/>.
/// </summary>
internal interface IEslDeviceServer
{
    /// <summary>
    /// Gets the ESL provider key (e.g., "Hanshow", "SoluM", "Stub") this implementation handles.
    /// Used by the rendered-integration consumer to filter messages to the correct vendor adapter.
    /// </summary>
    string Provider { get; }

    /// <summary>
    /// Dispatches a rendered image to the physical ESL display via the vendor device server.
    /// </summary>
    /// <param name="assignmentId">The owning display assignment identifier.</param>
    /// <param name="displayId">The target display identifier.</param>
    /// <param name="renderedImageUri">The rendered image URI (blob URI or local path).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Success or a delivery error with a diagnostic-friendly reason.</returns>
    ValueTask<ErrorOr<Success>> DispatchAsync(
        Guid assignmentId,
        Guid displayId,
        Uri renderedImageUri,
        CancellationToken cancellationToken);
}
