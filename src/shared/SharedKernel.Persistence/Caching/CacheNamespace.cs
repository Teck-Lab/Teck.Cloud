using SharedKernel.Core.MultiTenancy;

namespace SharedKernel.Persistence.Caching;

/// <summary>An explicit, versioned namespace for global or tenant cache entries and locks.</summary>
public sealed class CacheNamespace
{
    private readonly string _prefix;

    /// <summary>Initializes a new instance of the <see cref="CacheNamespace"/> class.</summary>
    /// <param name="service">The stable service identifier.</param>
    /// <param name="contract">The stable cached contract identifier.</param>
    /// <param name="version">The serialization/schema version.</param>
    /// <param name="scope">Explicit global or authorized tenant scope.</param>
    public CacheNamespace(string service, string contract, string version, DataScope scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(service);
        ArgumentException.ThrowIfNullOrWhiteSpace(contract);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(scope);
        Scope = scope;
        _prefix = string.Join("/", Escape(service), Escape(contract), Escape(version), scope.TenantId is { } tenant ? "tenant/" + Escape(tenant) : "global");
    }

    /// <summary>Gets the namespace ownership.</summary>
    public DataScope Scope { get; }

    /// <summary>Creates an unambiguous entry key.</summary>
    /// <param name="id">The invariantly formatted identifier.</param>
    /// <returns>The escaped key.</returns>
    public string Key(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _prefix + "/" + Escape(id);
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);
}
