using System.Security.Claims;
using Grpc.Core;
using Identity.TestHost.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.MultiTenancy;

namespace Identity.TestHost;

/// <summary>Administrative RPC explicitly targets one active tenant after native platform, resource and trusted-client authorization.</summary>
/// <param name="read">Tenant-filtered database.</param>
/// <param name="operation">The uninitialized request context.</param>
/// <param name="directory">The authoritative cached organization directory.</param>
public sealed class AdminRecordService(ProbeReadDbContext read, OperationContext operation, ITenantDirectory directory) : AdminRecords.AdminRecordsBase
{
    /// <inheritdoc/>
    [Authorize(Policy = "admin-read")]
    public override async Task<RecordReply> Read(AdminRecordRequest request, ServerCallContext context)
    {
        if (!Guid.TryParseExact(request.TenantId, "D", out var target)
            || !await directory.IsActiveAsync(target.ToString("D"), context.CancellationToken).ConfigureAwait(false))
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Active target tenant is required."));
        }

        operation.Initialize(DataScope.ForTenant(target.ToString("D")), context.GetHttpContext().User.FindFirstValue("sub"));
        var row = await read.Records.SingleOrDefaultAsync(record => record.Key == request.Key, context.CancellationToken).ConfigureAwait(false)
            ?? throw new RpcException(new Status(StatusCode.NotFound, "Record not found."));
        return new RecordReply { Key = row.Key, Value = row.Value, TenantId = row.TenantId, Actor = context.GetHttpContext().User.FindFirstValue("sub") ?? string.Empty };
    }
}
