using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter;

namespace SharedKernel.Infrastructure.Hosting;

internal sealed class OtlpTarget(Uri endpoint, OtlpExportProtocol protocol, string? headers)
{
    public Uri Endpoint { get; } = endpoint;

    public OtlpExportProtocol Protocol { get; } = protocol;

    public string? Headers { get; } = headers;

    public static OtlpTarget? Read(IConfiguration configuration, string signal, bool? enabled)
    {
        if (enabled is false)
        {
            return null;
        }

        var prefix = "OTEL_EXPORTER_OTLP_" + signal.ToUpperInvariant();
        var specific = configuration[prefix + "_ENDPOINT"];
        var endpoint = specific ?? configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        var protocol = (configuration[prefix + "_PROTOCOL"] ?? configuration["OTEL_EXPORTER_OTLP_PROTOCOL"] ?? "grpc") switch
        {
            "grpc" => OtlpExportProtocol.Grpc,
            "http/protobuf" => OtlpExportProtocol.HttpProtobuf,
            _ => throw new InvalidOperationException("OTLP protocol must be grpc or http/protobuf."),
        };
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length != 0)
        {
            throw new InvalidOperationException("OTLP endpoint must be an absolute HTTP(S) URI without embedded credentials.");
        }

        if (protocol == OtlpExportProtocol.HttpProtobuf && specific is null)
        {
            uri = new Uri(uri.AbsoluteUri.TrimEnd('/') + "/v1/" + signal, UriKind.Absolute);
        }

        return new OtlpTarget(uri, protocol, configuration[prefix + "_HEADERS"] ?? configuration["OTEL_EXPORTER_OTLP_HEADERS"]);
    }

    public void Configure(OtlpExporterOptions options)
    {
        options.Endpoint = Endpoint;
        options.Protocol = Protocol;
        options.Headers = Headers;
    }

    public Dictionary<string, string> HeaderDictionary()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in (Headers ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = header.Split('=', 2);
            if (pair.Length != 2 || string.IsNullOrWhiteSpace(pair[0]))
            {
                throw new InvalidOperationException("OTLP headers must use comma-separated name=value pairs.");
            }

            result[pair[0].Trim()] = Uri.UnescapeDataString(pair[1].Trim());
        }

        return result;
    }
}
