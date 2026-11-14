# .NET build quality

All C# projects inherit the root Directory.Build.props: production code, tests and
future Aspire hosts use the same warnings-as-errors policy in Debug and Release.
The SDK remains .NET 10; EF Core is pinned to the current 10.x patch rather than
silently moving the repository to .NET 11.

## Enforcement

- Compiler warnings, nullable diagnostics and enabled analyzer warnings fail builds.
- SDK code-quality analysis uses 10.0-recommended; security uses 10.0-all. Extra
  rules enforce source-generated logging, repeated enumeration checks, disposal
  and cancellation-token forwarding.
- Code style runs during builds. Enabled third-party suggestions are promoted to
  warnings. Disabled-by-default diagnostics are not blindly enabled: some express
  competing conventions. Specific additions belong in the root .editorconfig.
- NewStyleCop.Analyzers, Meziantou, Roslynator (including formatting), Sonar,
  Visual Studio Threading, SecurityCodeScan, ExceptionAnalyzer and ErrorProne.NET
  are installed as private build dependencies in every C# project.
- Keep ErrorProne.NET at the existing 0.9.0-beta.4 intentionally: its old stable
  version is not an upgrade. The other active analyzer pins use stable releases.
- Redis's analyzer is centrally pinned but only add its private PackageReference
  to an actual Redis consumer. CSharpGuidelines was already excluded by the old
  setup and is not reintroduced. SDK analyzers come from the pinned SDK; do not
  add the full Roslyn compiler/workspace packages just to run analysis.
- NuGet audits direct and transitive dependencies. Do not suppress audit warnings
  or downgrade a dependency merely to make restore pass.

The old monorepo's global analyzer-disable floor, test warning relaxations and
exclusion of *.g.cs from compilation are deliberately not carried forward.

## Style and documentation

Use file-scoped namespaces, System-first alphabetic usings outside namespaces,
four-space C# indentation, braces, var and single-line expression-bodied methods
where applicable. Keep the remaining enabled StyleCop spacing, layout, ordering,
file/type naming and public XML-documentation rules. Underscore-prefixed fields
are allowed; this. qualifiers and file copyright banners are not required.

stylecop.json configures public/exposed/interface documentation, not private or
internal implementation documentation. CS1591 is the sole root NoWarn exception:
FastEndpoints source generators emit undocumented public types. StyleCop SA1600,
SA1611, SA1615 and related rules enforce authored public API documentation instead.
The verification fixture proves missing documentation still fails the build.

Recognized generated files are marked generated_code for analyzer tooling, but
remain compilation inputs. Compiler errors and warnings are not broadly disabled
for generated code. Do not label authored code as generated to evade enforcement.
Do not globally disable suites, add broad NoWarn/WarningsNotAsErrors entries or
relax test builds. A justified false-positive suppression must be narrowly scoped,
documented and reviewed. Fix violations in source by default.

## Packages and source generators

Directory.Packages.props pins the selected FastEndpoints, Scalar, ErrorOr,
ServiceScan, Wolverine, FluentValidation, Mapperly, EF Core and specification
packages. Version pins alone do not install runtime dependencies into every layer.
Each consuming project declares its PackageReference without a Version attribute.

FastEndpoints.Generator, ServiceScan.SourceGenerator, Riok.Mapperly and
Microsoft.EntityFrameworkCore.Design references must use PrivateAssets=all.
ServiceScan.SourceGenerator is the package implementing the selected APIs; do not
also add the legacy ServiceScan package simply because it was centrally listed.
Do not add FastEndpoints messaging or another mediator beside Wolverine.

## Reproducible dependency restores

Every C# project records its complete dependency graph in packages.lock.json. Keep these
files in source control alongside Directory.Packages.props; central version pins do not
by themselves freeze transitive resolution. GITHUB_ACTIONS=true enables RestoreLockedMode
for every restore, including implicit restores through Nx, build and pack. CI also uses
an explicit dotnet restore Teck.Cloud.slnx --locked-mode before builds.

For an intentional package or project-reference change, update the central pin/reference,
run dotnet restore Teck.Cloud.slnx --force-evaluate locally, review the changed lockfiles,
and verify dotnet restore Teck.Cloud.slnx --locked-mode before committing. Never delete
locks or disable CI locked mode to conceal drift. SDK upgrades can change implicit
packages, so treat the SDK pin and affected locks as one reviewed change.

The analyzer-policy verifier creates disposable projects outside the repository. Those
synthetic fixtures alone opt out while establishing their initial lockfiles; it then proves
that locked restore succeeds unchanged and rejects an added dependency with NU1004.
Repository projects never inherit that exception.

NuGet owns the formatting of `packages.lock.json`. Biome still parses/lints these files,
but does not reformat them; regenerate them through restore rather than hand-editing them.

## ErrorOr at the HTTP boundary

Application command/query handlers return ErrorOr<T>. Invoke Wolverine using that
full result type. Standard FastEndpoints ExecuteAsync endpoints map successful
values to TypedResults and expected errors to a consistent Problem Details body.
A small shared mapping function is allowed; do not create custom endpoint base
classes or serialize ErrorOr itself as the external response contract.

Use validation -> 400, not-found -> 404, conflict -> 409 and forbidden -> 403 as
initial conventions. Authentication challenges stay with authentication middleware.
Preserve multiple validation errors; choose an explicit precedence for mixed errors.
Unexpected failures return a safe 500 response while retaining server-side evidence.
The shared ErrorOr mapper and HTTP fixture are implemented and tested. Add tests
for every service-specific response schema and Wolverine result path with its
first real consumer.

Scalar renders the generated OpenAPI contract. Document each possible error status
explicitly: a ProblemHttpResult response type alone cannot describe all statuses.
FastEndpoints validation and application errors should follow the same documented
Problem Details contract. Verify the JSON contexts used by typed HTTP results as
well as FastEndpoints' own serializer when enabling generated serialization.

## Verification

Run bun run verify:dotnet-policy. It copies the real configuration into a fresh
temporary workspace, checks clean source/test builds, proves representative bad
code fails with the intended diagnostic, and verifies generated files still compile.
It also restores the selected stack together and compiles a standard FastEndpoints
endpoint using ErrorOr/Wolverine plus actual ServiceScan-generated DI registration.

The fixtures are build-policy tests, not business-service integration tests. Their
sample error mapping is intentionally only a compilation probe, not a production
mapper. No test cluster, database or production service is created. The script
retains its temporary workspace and writes .artifacts/dotnet-policy-verification.json.

CI runs this policy check and the existing Nx affected builds. stylecop.json is a
shared Nx input, so changing policy invalidates cached builds. Real architecture
rules (such as read/write context separation) still need service architecture/DI
and behavior tests when services are introduced.

References:

- https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-options
- https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview
- https://github.com/DotNetAnalyzers/StyleCopAnalyzers/blob/master/documentation/Configuration.md
- https://fast-endpoints.com/docs/native-aot
- https://www.nuget.org/packages/ServiceScan.SourceGenerator/

## SharedKernel import exceptions

ICommand<TResponse> and IQuery<T> have a declaration-scoped S2326 suppression: the
response parameter deliberately forms a compile-time contract for handler constraints.
The generated xUnit/MTP startup files under obj have EPC29 disabled only for those
two filenames because their coverage-exclusion attribute has no justification.
Authored code and compiler diagnostics remain enforced.
