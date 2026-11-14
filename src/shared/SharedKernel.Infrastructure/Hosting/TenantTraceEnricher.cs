using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace SharedKernel.Infrastructure.Hosting;

internal sealed class TenantTraceEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (Activity.Current?.GetTagItem("tenant.id") is string tenant)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TenantId", tenant));
        }
    }
}
