using System.Text.Json.Serialization;

namespace Statistic.Api.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(Statistic.Domain.Statistics.StatSnapshot))]
[JsonSerializable(typeof(Statistic.Domain.Statistics.MonthlyMetric))]
[JsonSerializable(typeof(Statistic.Domain.Statistics.TenantMetric))]
[JsonSerializable(typeof(Statistic.Domain.Statistics.PieMetric))]
[JsonSerializable(typeof(Statistic.Domain.Statistics.ActivityEvent))]
[JsonSerializable(typeof(Statistic.Domain.Statistics.DisplayJobMetric))]
[JsonSerializable(typeof(Statistic.Domain.Statistics.DisplayJobDetail))]
[JsonSerializable(typeof(Statistic.Domain.Statistics.AccessPointMetric))]
[JsonSerializable(typeof(Statistic.Domain.Statistics.DashboardNotification))]
internal sealed partial class StatisticJsonSerializerContext : JsonSerializerContext
{
}
