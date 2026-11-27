using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SharedKernel.Core.Database;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Licensing.Infrastructure.Database;
using Teck.Cloud.Commerce.Orders.Infrastructure.Database;
using Wolverine.Runtime.Handlers;

namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>
/// Regenerates both commerce host handler trees inside one disposable candidate copy and proves the
/// ordinary Release build was already present and stayed untouched while the opt-in children ran.
/// </summary>
internal sealed class CommerceHandlerGeneration
{
    private const string RootVariable = "TECK_COMMERCE_CODEGEN_ROOT";

    private const string OrdersHostProject = "src/services/commerce/orders/Orders.Host/Orders.Host.csproj";

    private const string LicensingHostProject = "src/services/commerce/licensing/Licensing.Host/Licensing.Host.csproj";

    private const string AttestationPrefix = "Commerce codegen service-location verified: ";

    private const string ServiceLocationPrefix = "Commerce codegen service-location ";

    private const string RegistryGuardPrefix = "Commerce static Wolverine registry invalid:";

    private const int MaximumDiagnosticLines = 32;

    private const int MaximumDiagnosticLineLength = 8192;

    private const int MaximumEvaluationCharacters = 32 * 1024 * 1024;

    private const int MaximumErrorCharacters = 8192;

    private static readonly TimeSpan GeneratorTimeout = TimeSpan.FromSeconds(180);

    private static readonly TimeSpan EvaluationTimeout = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan CaptureDrainTimeout = TimeSpan.FromSeconds(15);

    private static readonly string[] DiagnosticPrefixes =
    [
        ServiceLocationPrefix,
        RegistryGuardPrefix,
        "Unhandled exception.",
    ];

    private static readonly JsonSerializerOptions EvidenceJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _root;

    private readonly CommerceStack _stack;

    internal CommerceHandlerGeneration(string root, CommerceStack stack)
    {
        _root = root;
        _stack = stack;
    }

    private enum CaptureMode
    {
        Raw = 0,
        Diagnostic = 1,
    }

    internal static string ResolveRoot()
    {
        var configured = Environment.GetEnvironmentVariable(RootVariable);
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(RootVariable + " must name an absolute disposable copy of the candidate source tree.");
        }

        if (!Path.IsPathFullyQualified(configured))
        {
            throw new InvalidOperationException(RootVariable + " must be an absolute path; the relative root '" + configured + "' is rejected.");
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured));
        var directory = new DirectoryInfo(root);
        if (!directory.Exists)
        {
            throw new InvalidOperationException(RootVariable + " does not exist as a directory at '" + root + "'.");
        }

        if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidOperationException(RootVariable + " must be a real disposable directory, not a symlink or reparse point.");
        }

        foreach (var relative in new[] { OrdersHostProject, LicensingHostProject })
        {
            if (!File.Exists(Path.Combine(root, relative)))
            {
                throw new InvalidOperationException(RootVariable + " does not contain the host project '" + relative + "'.");
            }
        }

        return root;
    }

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        var hosts = CreateHosts();
        var candidateSha = await ResolveCandidateShaAsync(cancellationToken).ConfigureAwait(false);
        var projects = await EvaluateClosureAsync(hosts, cancellationToken).ConfigureAwait(false);
        EnsureGeneratorExclusions(projects);
        var witnesses = CaptureNormalWitnesses(hosts);
        EnsureCompileItemsStayOutOfNormalBuilds(projects, witnesses);

        var generated = new List<GeneratedOutput>(hosts.Length);
        foreach (var host in hosts)
        {
            var environment = BuildGeneratorEnvironment(host, _stack);
            var execution = await ExecuteGeneratorAsync(host, environment, cancellationToken).ConfigureAwait(false);
            AssertGeneratorResult(host, execution);
            generated.Add(InspectGeneratedOutput(host, ExpectedAttestation(host)));
        }

        foreach (var witness in witnesses)
        {
            EnsureNormalWitnessUnchanged(witness);
        }

        await WriteEvidenceAsync(candidateSha, projects, witnesses, generated, cancellationToken).ConfigureAwait(false);
    }

    private static string ExpectedAttestation(HostGeneration host)
    {
        var names = host.ServiceLocationTypes.Order(StringComparer.Ordinal);
        return AttestationPrefix + host.AssemblyName + " NotAllowed " + string.Join(", ", names);
    }

    private static void AssertGeneratorResult(HostGeneration host, ProcessResult execution)
    {
        if (execution.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "The " + host.Name + " handler generator exited with code " + execution.ExitCode.ToString(CultureInfo.InvariantCulture)
                + " after " + execution.StandardOutput.LineCount.ToString(CultureInfo.InvariantCulture) + " output lines and "
                + execution.StandardError.LineCount.ToString(CultureInfo.InvariantCulture) + " error lines. Bounded diagnostics: "
                + SummarizeDiagnostics(execution) + ".");
        }

        var expected = ExpectedAttestation(host);
        var matches = 0;
        var serviceLocationLines = 0;
        foreach (var line in execution.StandardOutput.Lines.Concat(execution.StandardError.Lines))
        {
            var trimmed = line.Trim();
            if (string.Equals(trimmed, expected, StringComparison.Ordinal))
            {
                matches++;
            }

            if (trimmed.StartsWith(ServiceLocationPrefix, StringComparison.Ordinal))
            {
                serviceLocationLines++;
            }
        }

        if (matches != 1 || serviceLocationLines != 1)
        {
            throw new InvalidOperationException(
                "The " + host.Name + " generator did not attest exactly one '" + expected + "' line; it matched "
                + matches.ToString(CultureInfo.InvariantCulture) + " line(s) and emitted "
                + serviceLocationLines.ToString(CultureInfo.InvariantCulture) + " service-location line(s). Bounded diagnostics: "
                + SummarizeDiagnostics(execution) + ".");
        }
    }

    private static string SummarizeDiagnostics(ProcessResult execution)
    {
        var lines = execution.StandardOutput.Lines.Concat(execution.StandardError.Lines).ToArray();
        return lines.Length == 0 ? "(none)" : string.Join(" | ", lines);
    }

    private static Dictionary<string, string> BuildGeneratorEnvironment(HostGeneration host, CommerceStack stack)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var argument in host.Arguments(stack))
        {
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The " + host.Name + " fixture produced an argument that is not a '--Key:Path=value' setting.");
            }

            var body = argument[2..];
            var separator = body.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                throw new InvalidOperationException("The " + host.Name + " fixture produced a setting without a key/value separator; values are never reported.");
            }

            environment[body[..separator].Replace(":", "__", StringComparison.Ordinal)] = body[(separator + 1)..];
        }

        return environment;
    }

    private static async Task<ProcessResult> ExecuteGeneratorAsync(
        HostGeneration host,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken)
    {
        string[] arguments =
        [
            "run",
            "--project",
            host.ProjectPath,
            "--no-launch-profile",
            "-c",
            "Release",
            "-p:CommerceWolverineCodegen=true",
            "-p:RestoreLockedMode=true",
            "--",
            "codegen",
            "write",
        ];
        try
        {
            return await ExecuteDiagnosticAsync("dotnet", arguments, ProjectDirectory(host), environment, GeneratorTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException(
                "The " + host.Name + " handler generator exceeded its " + GeneratorTimeout.TotalSeconds.ToString(CultureInfo.InvariantCulture)
                + "-second bound; only its owned process tree was killed.",
                exception);
        }
    }

    private static Task<ProcessResult> ExecuteRawAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        int maximumCharacters,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            fileName,
            arguments,
            workingDirectory,
            environment: null,
            timeout,
            CaptureMode.Raw,
            maximumCharacters,
            cancellationToken);

    private static Task<ProcessResult> ExecuteDiagnosticAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            fileName,
            arguments,
            workingDirectory,
            environment,
            timeout,
            CaptureMode.Diagnostic,
            MaximumDiagnosticLineLength,
            cancellationToken);

    private static async Task<ProcessResult> ExecuteAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environment,
        TimeSpan timeout,
        CaptureMode captureMode,
        int maximumCharacters,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                startInfo.Environment[key] = value;
            }
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("The '" + fileName + "' process could not start in '" + workingDirectory + "'.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var standardOutput = CaptureAsync(process.StandardOutput, captureMode, maximumCharacters, deadline.Token);
        var standardError = CaptureAsync(process.StandardError, captureMode, MaximumErrorCharacters, deadline.Token);
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            await KillOwnedProcessTreeAsync(process).ConfigureAwait(false);
            _ = await DrainAsync(standardOutput, standardError).ConfigureAwait(false);
            throw new TimeoutException(
                "The '" + fileName + "' process in '" + workingDirectory + "' exceeded its "
                + timeout.TotalSeconds.ToString(CultureInfo.InvariantCulture) + "-second bound; only its owned process tree was killed.",
                exception);
        }
        catch (OperationCanceledException)
        {
            await KillOwnedProcessTreeAsync(process).ConfigureAwait(false);
            _ = await DrainAsync(standardOutput, standardError).ConfigureAwait(false);
            throw;
        }

        var (output, error) = await DrainAsync(standardOutput, standardError).ConfigureAwait(false);
        return new ProcessResult(process.ExitCode, output, error);
    }

    private static Task<CapturedStream> CaptureAsync(
        TextReader reader,
        CaptureMode captureMode,
        int maximumCharacters,
        CancellationToken cancellationToken)
    {
        return captureMode == CaptureMode.Raw
            ? CaptureRawAsync(reader, maximumCharacters, cancellationToken)
            : CaptureDiagnosticsAsync(reader, cancellationToken);
    }

    private static async Task<CapturedStream> CaptureRawAsync(
        TextReader reader,
        int maximumCharacters,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var buffer = new char[8192];
        var truncated = false;
        try
        {
            while (true)
            {
                var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                var remaining = maximumCharacters - builder.Length;
                if (remaining <= 0)
                {
                    truncated = true;
                    continue;
                }

                _ = builder.Append(buffer, 0, Math.Min(read, remaining));
                truncated |= read > remaining;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            truncated = true;
        }
        catch (IOException)
        {
            truncated = true;
        }

        return new CapturedStream(builder.ToString(), [], 0, truncated);
    }

    private static async Task<CapturedStream> CaptureDiagnosticsAsync(TextReader reader, CancellationToken cancellationToken)
    {
        var lines = new List<string>(MaximumDiagnosticLines);
        var lineCount = 0;
        var truncated = false;
        try
        {
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                lineCount++;
                if (!IsDiagnosticLine(line))
                {
                    continue;
                }

                if (lines.Count >= MaximumDiagnosticLines)
                {
                    truncated = true;
                    continue;
                }

                lines.Add(line.Length > MaximumDiagnosticLineLength ? "<<diagnostic line exceeded the capture bound>>" : line);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            truncated = true;
        }
        catch (IOException)
        {
            truncated = true;
        }

        return new CapturedStream(string.Empty, lines, lineCount, truncated);
    }

    private static bool IsDiagnosticLine(string line)
    {
        var trimmed = line.AsSpan().TrimStart();
        foreach (var prefix in DiagnosticPrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task KillOwnedProcessTreeAsync(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The owned process already exited between the deadline and the kill.
        }
        catch (NotSupportedException)
        {
            // The owned process has no killable tree on this platform; the bounded exit wait below decides.
        }

        using var exitDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await process.WaitForExitAsync(exitDeadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (exitDeadline.IsCancellationRequested)
        {
            // A bounded best-effort wait; the timeout diagnostics already fail the fixture.
        }
    }

    private static async Task<(CapturedStream Output, CapturedStream Error)> DrainAsync(
        Task<CapturedStream> output,
        Task<CapturedStream> error)
    {
        CapturedStream standardOutput;
        try
        {
            standardOutput = await output.WaitAsync(CaptureDrainTimeout, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            standardOutput = new CapturedStream(string.Empty, [], 0, Truncated: true);
        }

        CapturedStream standardError;
        try
        {
            standardError = await error.WaitAsync(CaptureDrainTimeout, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            standardError = new CapturedStream(string.Empty, [], 0, Truncated: true);
        }

        return (standardOutput, standardError);
    }

    private static List<string> ReadItemValues(JsonElement items, string itemType, string projectDirectory)
    {
        if (!items.TryGetProperty(itemType, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var values = new List<string>(array.GetArrayLength());
        foreach (var item in array.EnumerateArray())
        {
            var value = ItemValue(item)
                ?? throw new InvalidOperationException("An evaluated '" + itemType + "' item has no usable FullPath or Identity.");
            values.Add(Path.GetFullPath(value, projectDirectory));
        }

        return values;
    }

    private static string? ItemValue(JsonElement item) => item switch
    {
        { ValueKind: JsonValueKind.String } => item.GetString(),
        { ValueKind: JsonValueKind.Object } => StringProperty(item, "FullPath") ?? StringProperty(item, "Identity"),
        _ => null,
    };

    private static string? StringProperty(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static void EnsureGeneratorExclusions(IReadOnlyList<EvaluatedProject> projects)
    {
        foreach (var project in projects)
        {
            var tokens = project.DefaultItemExcludes.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!tokens.Contains("bin/**", StringComparer.Ordinal) || !tokens.Contains("obj/**", StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "Opt-in evaluation of '" + project.ProjectPath + "' did not exclude the project-local bin/** and obj/** build trees.");
            }
        }
    }

    private static GeneratedOutput InspectGeneratedOutput(HostGeneration host, string attestation)
    {
        var directory = Path.Combine(ProjectDirectory(host), "Internal", "Generated");
        if (!Directory.Exists(directory))
        {
            throw new InvalidOperationException("The " + host.Name + " generator produced no Internal/Generated directory.");
        }

        var files = EnumerateGeneratedFiles(directory);
        const string registryName = HandlerRegistry.GeneratedTypeName + ".cs";
        var registries = files.Where(file => string.Equals(Path.GetFileName(file), registryName, StringComparison.Ordinal)).ToArray();
        if (registries.Length != 1)
        {
            throw new InvalidOperationException(
                "The " + host.Name + " generator produced " + registries.Length.ToString(CultureInfo.InvariantCulture)
                + " '" + registryName + "' files; exactly one registry is required.");
        }

        var registry = registries[0];
        var handlers = files.Where(file => !string.Equals(file, registry, StringComparison.Ordinal)).ToArray();
        if (handlers.Length == 0)
        {
            throw new InvalidOperationException("The " + host.Name + " generator produced no handler sources beside the registry.");
        }

        var contents = new Dictionary<string, string>(files.Count, StringComparer.Ordinal);
        var evidence = new List<GeneratedFile>(files.Count);
        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(file);
            contents[file] = Encoding.UTF8.GetString(bytes);
            evidence.Add(new GeneratedFile(ToRelativePath(ProjectDirectory(host), file), Convert.ToHexStringLower(SHA256.HashData(bytes))));
        }

        var registryText = contents[registry];
        if (!registryText.Contains("class " + HandlerRegistry.GeneratedTypeName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The " + host.Name + " registry source does not declare '" + HandlerRegistry.GeneratedTypeName + "'.");
        }

        ValidateRegistryAccessors(host, registryText);

        return handlers.Any(file => contents[file].Contains(host.ExpectedHandlerIdentifier, StringComparison.Ordinal))
            ? new GeneratedOutput(
                host.Name,
                attestation,
                ToRelativePath(ProjectDirectory(host), registry),
                handlers.Length,
                evidence)
            : throw new InvalidOperationException(
                "No " + host.Name + " generated handler references the production '" + host.ExpectedHandlerIdentifier + "' handler.");
    }

    private static void ValidateRegistryAccessors(HostGeneration host, string registryText)
    {
        if (!registryText.Contains("HandlerTypes", StringComparison.Ordinal)
            || !registryText.Contains("MessageTypes", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The " + host.Name + " registry source is missing the HandlerTypes/MessageTypes accessors.");
        }
    }

    private static List<string> EnumerateGeneratedFiles(string directory)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.None,
        };
        var files = new List<string>();
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory, "*", options))
        {
            var attributes = File.GetAttributes(entry);
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidOperationException("Generated output contains a symlink or reparse point at '" + entry + "'.");
            }

            if (attributes.HasFlag(FileAttributes.Directory))
            {
                continue;
            }

            if (!string.Equals(Path.GetExtension(entry), ".cs", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Generated output contains a non-C# file at '" + entry + "'.");
            }

            files.Add(entry);
        }

        files.Sort(StringComparer.Ordinal);
        return files;
    }

    private static string ToRelativePath(string basePath, string path) =>
        Path.GetRelativePath(basePath, path).Replace(Path.DirectorySeparatorChar, '/');

    private static string ProjectDirectory(HostGeneration host) =>
        Path.GetDirectoryName(host.ProjectPath)
        ?? throw new InvalidOperationException("The " + host.Name + " host project has no directory.");

    private static string HostAssemblyName(Type hostType, string name) =>
        hostType.Assembly.GetName().Name
        ?? throw new InvalidOperationException("The " + name + " host assembly has no simple name.");

    private static bool IsUnder(string path, string root)
    {
        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        return fullPath.Length > fullRoot.Length
            && fullPath.StartsWith(fullRoot, StringComparison.Ordinal)
            && fullPath[fullRoot.Length] == Path.DirectorySeparatorChar;
    }

    private static string FileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static bool IsLowercaseHex(char value) => value is (>= '0' and <= '9') or (>= 'a' and <= 'f');

    private HostGeneration[] CreateHosts()
    {
        var ordersAssembly = HostAssemblyName(typeof(Teck.Cloud.Commerce.Orders.Host.OrdersHost), "Orders");
        var licensingAssembly = HostAssemblyName(typeof(Teck.Cloud.Commerce.Licensing.Host.LicensingHost), "Licensing");
        var hosts = new HostGeneration[]
        {
            new(
                "Orders",
                Path.Combine(_root, OrdersHostProject),
                ordersAssembly,
                [
                    typeof(OperationContext).FullName!,
                    typeof(ITenantDirectory).FullName!,
                    typeof(OrdersReadDbContext).FullName!,
                    typeof(OrdersWriteDbContext).FullName!,
                    typeof(IUnitOfWork).FullName!,
                ],
                "CheckCurrentEntitlementHandler",
                static stack => CommerceTestConfiguration.Orders(
                    stack.Keycloak,
                    stack.Postgres,
                    stack.Rabbit,
                    stack.Stripe,
                    stack.OrdersAddress,
                    stack.OrdersWebhookAddress)),
            new(
                "Licensing",
                Path.Combine(_root, LicensingHostProject),
                licensingAssembly,
                [
                    typeof(OperationContext).FullName!,
                    typeof(ITenantDirectory).FullName!,
                    typeof(Teck.Cloud.Commerce.Orders.V1.Orders.OrdersClient).FullName!,
                    typeof(LicensingReadDbContext).FullName!,
                    typeof(LicensingWriteDbContext).FullName!,
                    typeof(IUnitOfWork).FullName!,
                ],
                "GetCurrentLicenseHandler",
                static stack => CommerceTestConfiguration.Licensing(
                    stack.Keycloak,
                    stack.Postgres,
                    stack.Rabbit,
                    stack.Redis,
                    stack.LicensingAddress,
                    stack.OrdersAddress,
                    stack.KeyRing.K1.KeyId,
                    stack.KeyRing.Initial())),
        };
        foreach (var host in hosts)
        {
            if (!File.Exists(host.ProjectPath))
            {
                throw new InvalidOperationException(
                    "The " + RootVariable + " copy is missing the " + host.Name + " host project at '" + host.ProjectPath + "'.");
            }
        }

        return hosts;
    }

    private async Task<string> ResolveCandidateShaAsync(CancellationToken cancellationToken)
    {
        var metadata = Path.Combine(_root, ".git");
        if (!Directory.Exists(metadata) && !File.Exists(metadata))
        {
            return "unavailable: the disposable copy carries no git metadata";
        }

        var execution = await ExecuteRawAsync(
            "git",
            ["-C", _root, "rev-parse", "HEAD"],
            _root,
            MaximumErrorCharacters,
            GitTimeout,
            cancellationToken).ConfigureAwait(false);
        if (execution.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "git rev-parse HEAD failed in the disposable copy with exit code "
                + execution.ExitCode.ToString(CultureInfo.InvariantCulture) + ": " + execution.StandardError.Text.Trim());
        }

        var sha = execution.StandardOutput.Text.Trim();
        return sha.Length == 40 && sha.All(IsLowercaseHex)
            ? sha
            : throw new InvalidOperationException("git rev-parse HEAD did not report an exact 40-character lowercase commit in the disposable copy.");
    }

    private async Task<IReadOnlyList<EvaluatedProject>> EvaluateClosureAsync(
        IReadOnlyList<HostGeneration> hosts,
        CancellationToken cancellationToken)
    {
        var visited = new Dictionary<string, EvaluatedProject>(StringComparer.Ordinal);
        var pending = new Queue<string>();
        foreach (var host in hosts)
        {
            pending.Enqueue(Path.GetFullPath(host.ProjectPath));
        }

        while (pending.Count > 0)
        {
            var projectPath = pending.Dequeue();
            if (visited.ContainsKey(projectPath))
            {
                continue;
            }

            var project = await EvaluateProjectAsync(projectPath, cancellationToken).ConfigureAwait(false);
            visited.Add(projectPath, project);
            foreach (var reference in project.ProjectReferences)
            {
                if (reference.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) && !visited.ContainsKey(reference))
                {
                    pending.Enqueue(reference);
                }
            }
        }

        return [.. visited.Values.OrderBy(project => project.ProjectPath, StringComparer.Ordinal)];
    }

    private async Task<EvaluatedProject> EvaluateProjectAsync(string projectPath, CancellationToken cancellationToken)
    {
        var execution = await ExecuteRawAsync(
            "dotnet",
            [
                "msbuild",
                projectPath,
                "-nologo",
                "-p:Configuration=Release",
                "-p:CommerceWolverineCodegen=true",
                "-getItem:ProjectReference,Compile",
                "-getProperty:DefaultItemExcludes",
            ],
            _root,
            MaximumEvaluationCharacters,
            EvaluationTimeout,
            cancellationToken).ConfigureAwait(false);
        if (execution.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Opt-in evaluation of '" + Relative(projectPath) + "' failed with exit code "
                + execution.ExitCode.ToString(CultureInfo.InvariantCulture) + ": " + execution.StandardError.Text.Trim());
        }

        if (execution.StandardOutput.Truncated)
        {
            throw new InvalidOperationException("Opt-in evaluation of '" + Relative(projectPath) + "' exceeded the output bound.");
        }

        try
        {
            using var document = JsonDocument.Parse(execution.StandardOutput.Text);
            var element = document.RootElement;
            if (!element.TryGetProperty("Items", out var items)
                || !element.TryGetProperty("Properties", out var properties)
                || !properties.TryGetProperty("DefaultItemExcludes", out var excludes)
                || excludes.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    "Opt-in evaluation of '" + Relative(projectPath) + "' did not report both items and DefaultItemExcludes.");
            }

            var directory = Path.GetDirectoryName(projectPath)
                ?? throw new InvalidOperationException("The project '" + projectPath + "' has no directory.");
            return new EvaluatedProject(
                projectPath,
                ReadItemValues(items, "Compile", directory),
                ReadItemValues(items, "ProjectReference", directory),
                excludes.GetString() ?? string.Empty);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "Opt-in evaluation of '" + Relative(projectPath) + "' produced malformed JSON: " + exception.Message,
                exception);
        }
    }

    private List<NormalWitness> CaptureNormalWitnesses(IReadOnlyList<HostGeneration> hosts)
    {
        var witnesses = new List<NormalWitness>(hosts.Count);
        foreach (var host in hosts)
        {
            var directory = Path.Combine(ProjectDirectory(host), "obj", "Release");
            var candidates = new List<string>();
            if (Directory.Exists(directory))
            {
                candidates.AddRange(Directory.EnumerateFiles(directory, "*AssemblyInfo.cs", SearchOption.AllDirectories));
                candidates.AddRange(Directory.EnumerateFiles(directory, "*AssemblyAttributes.cs", SearchOption.AllDirectories));
            }

            if (candidates.Count == 0)
            {
                throw new InvalidOperationException(
                    "The ordinary Release build left no AssemblyInfo witness under '" + Relative(directory)
                    + "' for the " + host.Name + " host; the opt-in generator requires the normal build outputs in the same tree.");
            }

            candidates.Sort(StringComparer.Ordinal);
            var witness = candidates[0];
            witnesses.Add(new NormalWitness(host.Name, Relative(witness), FileSha256(witness)));
        }

        return witnesses;
    }

    private void EnsureCompileItemsStayOutOfNormalBuilds(
        IReadOnlyList<EvaluatedProject> projects,
        IReadOnlyList<NormalWitness> witnesses)
    {
        var normalRoots = new List<string>(projects.Count * 2);
        foreach (var project in projects)
        {
            var directory = Path.GetDirectoryName(project.ProjectPath)
                ?? throw new InvalidOperationException("The project '" + project.ProjectPath + "' has no directory.");
            normalRoots.Add(Path.Combine(directory, "bin"));
            normalRoots.Add(Path.Combine(directory, "obj"));
        }

        foreach (var project in projects)
        {
            foreach (var compile in project.CompileItems)
            {
                foreach (var root in normalRoots)
                {
                    if (IsUnder(compile, root))
                    {
                        throw new InvalidOperationException(
                            "Opt-in evaluation of '" + Relative(project.ProjectPath) + "' included the compile item '"
                            + Relative(compile) + "' from the normal build tree '" + Relative(root) + "'.");
                    }
                }
            }
        }

        var compilePaths = new HashSet<string>(projects.SelectMany(project => project.CompileItems), StringComparer.Ordinal);
        foreach (var witness in witnesses)
        {
            if (compilePaths.Contains(Path.GetFullPath(Path.Combine(_root, witness.RelativePath))))
            {
                throw new InvalidOperationException(
                    "The normal build witness '" + witness.RelativePath + "' for the " + witness.Host + " host was compiled by the opt-in evaluation.");
            }
        }
    }

    private void EnsureNormalWitnessUnchanged(NormalWitness witness)
    {
        var path = Path.GetFullPath(Path.Combine(_root, witness.RelativePath));
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                "The normal build witness '" + witness.RelativePath + "' for the " + witness.Host + " host disappeared during handler generation.");
        }

        var current = FileSha256(path);
        if (!string.Equals(current, witness.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The normal build witness '" + witness.RelativePath + "' for the " + witness.Host
                + " host changed during handler generation (" + witness.Sha256 + " -> " + current + ").");
        }
    }

    private Task WriteEvidenceAsync(
        string candidateSha,
        IReadOnlyList<EvaluatedProject> projects,
        IReadOnlyList<NormalWitness> witnesses,
        IReadOnlyList<GeneratedOutput> generated,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(_root, ".artifacts", "commerce-handler-generation");
        _ = Directory.CreateDirectory(directory);
        var evidence = new GenerationEvidence(
            candidateSha,
            _root,
            [.. projects.Select(project => Relative(project.ProjectPath))],
            witnesses,
            generated);
        var path = Path.Combine(directory, "evidence.json");
        return File.WriteAllTextAsync(path, JsonSerializer.Serialize(evidence, EvidenceJson), cancellationToken);
    }

    private string Relative(string path) => ToRelativePath(_root, path);

    private sealed record HostGeneration(
        string Name,
        string ProjectPath,
        string AssemblyName,
        IReadOnlyList<string> ServiceLocationTypes,
        string ExpectedHandlerIdentifier,
        Func<CommerceStack, string[]> Arguments);

    private sealed record EvaluatedProject(
        string ProjectPath,
        IReadOnlyList<string> CompileItems,
        IReadOnlyList<string> ProjectReferences,
        string DefaultItemExcludes);

    private sealed record NormalWitness(string Host, string RelativePath, string Sha256);

    private sealed record GeneratedFile(string Path, string Sha256);

    private sealed record GeneratedOutput(
        string Host,
        string Attestation,
        string RegistryFile,
        int HandlerFiles,
        IReadOnlyList<GeneratedFile> Files);

    private sealed record GenerationEvidence(
        string CandidateSha,
        string CodegenRoot,
        IReadOnlyList<string> Projects,
        IReadOnlyList<NormalWitness> NormalWitnesses,
        IReadOnlyList<GeneratedOutput> Generated);

    private sealed record CapturedStream(string Text, IReadOnlyList<string> Lines, int LineCount, bool Truncated);

    private sealed record ProcessResult(int ExitCode, CapturedStream StandardOutput, CapturedStream StandardError);
}
