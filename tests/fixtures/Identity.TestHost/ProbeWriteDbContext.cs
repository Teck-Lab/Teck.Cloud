using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.MultiTenancy;

namespace Identity.TestHost;

/// <summary>Fixture write context.</summary>
/// <param name="options">Provider configuration.</param>
/// <param name="operation">Validated request scope.</param>
public sealed class ProbeWriteDbContext(DbContextOptions<ProbeWriteDbContext> options, OperationContext operation) : ProbeDbContext(options, operation, readOnly: false);
