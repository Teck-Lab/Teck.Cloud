using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.MultiTenancy;

namespace Identity.TestHost;

/// <summary>Fixture read-only context.</summary>
/// <param name="options">Provider configuration.</param>
/// <param name="operation">Validated request scope.</param>
public sealed class ProbeReadDbContext(DbContextOptions<ProbeReadDbContext> options, OperationContext operation) : ProbeDbContext(options, operation, readOnly: true);
