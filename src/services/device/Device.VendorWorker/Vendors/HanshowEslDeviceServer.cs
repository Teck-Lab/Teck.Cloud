// <copyright file="HanshowEslDeviceServer.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;

namespace Device.VendorWorker.Vendors;

/// <summary>
/// Scaffold for the Hanshow vendor adapter. Returns a not-implemented error to preserve the routing
/// seam and exercise the failure → MarkFailed flow until the real Hanshow SDK integration lands.
/// </summary>
internal sealed partial class HanshowEslDeviceServer : IEslDeviceServer
{
    /// <summary>
    /// The provider key advertised by this adapter.
    /// </summary>
    public const string ProviderKey = "Hanshow";

    private readonly ILogger<HanshowEslDeviceServer> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HanshowEslDeviceServer"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public HanshowEslDeviceServer(ILogger<HanshowEslDeviceServer> logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string Provider => ProviderKey;

    /// <inheritdoc/>
    public ValueTask<ErrorOr<Success>> DispatchAsync(
        Guid assignmentId,
        Guid displayId,
        Uri renderedImageUri,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(renderedImageUri);
        cancellationToken.ThrowIfCancellationRequested();

        LogNotImplemented(this.logger, assignmentId, displayId);

        return ValueTask.FromResult<ErrorOr<Success>>(
            Error.Unexpected("Hanshow.NotImplemented", "Hanshow vendor adapter not yet implemented."));
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "[HanshowEsl] Dispatch not implemented for assignment {AssignmentId} on display {DisplayId}")]
    private static partial void LogNotImplemented(ILogger logger, Guid assignmentId, Guid displayId);
}
