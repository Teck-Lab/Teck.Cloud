using System.Security.Claims;
using Grpc.Core;
using Identity.TestHost.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.MultiTenancy;

namespace Identity.TestHost;

/// <summary>Actual HTTP/2 fixture endpoint with native resource permissions and tenant-filtered persistence.</summary>
/// <param name="write">Tenant-guarded write context.</param>
/// <param name="read">Tenant-filtered read context.</param>
/// <param name="operation">Validated organization identity.</param>
public sealed class RecordService(ProbeWriteDbContext write, ProbeReadDbContext read, OperationContext operation) : Records.RecordsBase
{
    /// <inheritdoc/>
    [Authorize(Policy = "records-write")]
    public override async Task<RecordReply> Store(RecordRequest request, ServerCallContext context)
    {
        var record = await write.Records.SingleOrDefaultAsync(row => row.Key == request.Key, context.CancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            record = new ProbeRecord { Key = request.Key, Value = request.Value, TenantId = operation.Scope.TenantId! };
            _ = write.Records.Add(record);
        }
        else
        {
            record.Value = request.Value;
        }

        _ = await write.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
        return Reply(record, context);
    }

    /// <inheritdoc/>
    [Authorize(Policy = "records-read")]
    public override async Task<RecordReply> Read(RecordRequest request, ServerCallContext context)
    {
        var record = await read.Records.SingleOrDefaultAsync(row => row.Key == request.Key, context.CancellationToken).ConfigureAwait(false)
            ?? throw new RpcException(new Status(StatusCode.NotFound, "Record not found."));
        return Reply(record, context);
    }

    private static RecordReply Reply(ProbeRecord record, ServerCallContext context) => new()
    {
        Key = record.Key,
        Value = record.Value,
        TenantId = record.TenantId,
        Actor = context.GetHttpContext().User.FindFirstValue("sub") ?? string.Empty,
    };
}
