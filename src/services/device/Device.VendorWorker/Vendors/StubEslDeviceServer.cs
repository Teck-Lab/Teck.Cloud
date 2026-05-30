// <copyright file="StubEslDeviceServer.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;

namespace Device.VendorWorker.Vendors;

/// <summary>
/// In-memory stub vendor adapter that logs the dispatch and returns success.
/// Used in dev/test to exercise the full rendered → dispatched → delivered flow without a real vendor SDK.
/// </summary>
internal sealed partial class StubEslDeviceServer : IEslDeviceServer
{
    /// <summary>
    /// The provider key that the integration event's EslProvider field must match
    /// for this adapter to process the message.
    /// </summary>
    public const string ProviderKey = "Stub";

    private readonly ILogger<StubEslDeviceServer> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubEslDeviceServer"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public StubEslDeviceServer(ILogger<StubEslDeviceServer> logger)
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

        LogDispatch(this.logger, assignmentId, displayId, renderedImageUri);

        return ValueTask.FromResult<ErrorOr<Success>>(Result.Success);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "[StubEsl] Dispatched assignment {AssignmentId} to display {DisplayId} with image {RenderedImageUri}")]
    private static partial void LogDispatch(ILogger logger, Guid assignmentId, Guid displayId, Uri renderedImageUri);
}
