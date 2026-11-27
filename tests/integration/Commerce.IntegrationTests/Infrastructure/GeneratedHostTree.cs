using System.Security.Cryptography;
using System.Text;
using Shouldly;

namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>File-exact view of one host's committed generated handler sources.</summary>
internal sealed class GeneratedHostTree
{
    internal const string RegistryFileName = "GeneratedHandlerRegistry.cs";

    private readonly IReadOnlyDictionary<string, byte[]> _bytes;

    private GeneratedHostTree(IReadOnlyDictionary<string, byte[]> bytes, string registryPath)
    {
        _bytes = bytes;
        RegistryPath = registryPath;
        Hashes = BuildHashes(bytes);
    }

    /// <summary>Gets the host-relative path of the unique generated handler registry source.</summary>
    internal string RegistryPath { get; }

    internal IReadOnlyDictionary<string, string> Hashes { get; }

    internal int FileCount => _bytes.Count;

    internal IReadOnlyList<string> Files => [.. _bytes.Keys.Order(StringComparer.Ordinal)];

    internal string RegistryText => Text(RegistryPath);

    internal static GeneratedHostTree Read(string repositoryRoot, CommerceStaticHost host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentNullException.ThrowIfNull(host);
        var root = Path.Combine(repositoryRoot, host.GeneratedRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.Exists(root).ShouldBeTrue(host.Name + " generated sources are missing at " + root);
        var bytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            bytes[relative] = File.ReadAllBytes(file);
        }

        var registries = bytes.Keys
            .Where(key => string.Equals(Path.GetFileName(key), RegistryFileName, StringComparison.Ordinal))
            .ToArray();
        registries.Length.ShouldBe(1, host.Name + " must contain exactly one " + RegistryFileName);
        return new GeneratedHostTree(bytes, registries[0]);
    }

    internal bool Contains(string relativePath) => _bytes.ContainsKey(relativePath);

    internal string Hash(string relativePath) => Hashes[relativePath];

    internal string Text(string relativePath) => Encoding.UTF8.GetString(_bytes[relativePath]);

    private static Dictionary<string, string> BuildHashes(IReadOnlyDictionary<string, byte[]> bytes)
    {
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in bytes)
        {
            hashes[pair.Key] = Convert.ToHexString(SHA256.HashData(pair.Value));
        }

        return hashes;
    }
}
