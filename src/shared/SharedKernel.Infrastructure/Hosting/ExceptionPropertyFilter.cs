using Serilog.Exceptions.Filters;

namespace SharedKernel.Infrastructure.Hosting;

internal sealed class ExceptionPropertyFilter : IExceptionPropertyFilter
{
    public bool ShouldPropertyBeFiltered(Exception exception, string propertyName, object? value) =>
        string.Equals(propertyName, "Entries", StringComparison.Ordinal)
        || string.Equals(propertyName, "Data", StringComparison.Ordinal)
        || propertyName.StartsWith("Trailers.", StringComparison.Ordinal);
}
