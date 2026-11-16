# Test tooling and generated data

For .NET test projects, use xUnit v3 on Microsoft Testing Platform (MTP). Shouldly is
the assertion API for both existing and new .NET tests. xUnit supplies test attributes,
fixtures and lifecycle; use Shouldly for assertions. These packages are test dependencies,
never service runtime dependencies. Repository tooling tests are a separate Node.js lane;
see [test layers](testing.md#test-layers).

## Packages adopted from Teck.Monorepo

| Purpose                                      | Package                                                        | Use in Teck.Cloud                                                   |
| -------------------------------------------- | -------------------------------------------------------------- | ------------------------------------------------------------------- |
| Assertions                                   | Shouldly 4.3.0                                                 | All executable test projects                                        |
| Mocks and interaction checks                 | NSubstitute 6.2.0                                              | Core/Infrastructure unit tests and Infrastructure integration tests |
| Mock misuse detection                        | NSubstitute.Analyzers.CSharp 1.0.17                            | Projects referencing NSubstitute; private analyzer dependency       |
| Arbitrary object graphs and SUT construction | AutoFixture 5.0.0-rc.1                                         | Unit tests and Infrastructure integration tests                     |
| Realistic data with explicit rules           | Bogus 35.6.5                                                   | Same test projects                                                  |
| Automatic population of DTOs                  | Soenneker.Utils.AutoBogus 11.0.1011                            | Same test projects; compatible .NET 10 assets                       |
| AutoBogus interface/abstract substitutes     | Soenneker.Utils.AutoBogus.NSubstitute 11.0.880                 | Same test projects                                                  |
| Endpoint fixtures                            | FastEndpoints.Testing 9.0.0                                    | Infrastructure endpoint integration tests                           |
| Architecture rules                           | TngTech.ArchUnitNET and TngTech.ArchUnitNET.xUnitV3 0.13.5     | SharedKernel.ArchitectureTests                                      |
| Coverage                                     | coverlet.MTP 10.1.0                                            | All executable test projects                                        |
| Real dependencies                            | Testcontainers PostgreSql, Redis, RabbitMq and Keycloak 4.16.0 | Integration projects as needed                                      |
| Whole-stack lifecycle                        | Aspire.Hosting.Testing 13.6.5                                  | Application E2E                                                     |

The old repository pins Shouldly, AutoFixture and Bogus/AutoBogus, but its test projects do
not actually reference all those pins. NSubstitute and ArchUnitNET are actively referenced.
The new setup explicitly references the tools above, with examples in existing tests.

FastEndpoints.Testing requires xunit.v3.extensibility.core 4.0.3, so the xunit.v3 package is
aligned to 4.0.3 throughout the solution. Keep Testcontainers modules on the same version.

Do not copy the old VSTest adapter/SDK or coverlet.collector into MTP projects. coverlet.MTP
provides the equivalent coverage integration for this runner. Microsoft.AspNetCore.TestHost
is already available through Microsoft.AspNetCore.Mvc.Testing. Do not use EF's InMemory
provider to claim relational query, migration, transaction, constraint or tenancy correctness.

AutoFixture and AutoFixture.AutoNSubstitute use the explicitly approved 5.0.0-rc.1 release.
This adapter supports NSubstitute >= 5 and < 7, including our 6.2.0. The older stable adapter
4.18.1 excludes NSubstitute 6. Keep both AutoFixture packages on the same version.

```csharp
var fixture = new Fixture().Customize(new AutoNSubstituteCustomization());
var tokens = fixture.Freeze<IAccessTokenProvider>();
// Configure the behavior under test explicitly, then create the SUT with fixture.Create<T>().
```

Freeze reuses the same substitute across SUT construction and assertions. Explicit Inject
remains available when a test already owns a substitute. Avoid enabling automatic member
configuration when default generated responses could hide a missing setup. TenantConnectionTests
exercises the approved adapter against the real tenant connection-selection behavior.

## Mocking boundaries

Use NSubstitute for interfaces at unit-test boundaries: external clients, clocks, token
providers and repositories when testing handler decisions independently. Configure relevant
returns explicitly and verify important interactions with Received/DidNotReceive. Do not
mock the class under test, non-virtual concrete members, DbContext, DbSet or IQueryable.
Use the real Wolverine pipeline and real PostgreSQL/RabbitMQ for transport and persistence
integration claims. A substituted external service is acceptable only when that service is
outside the test's stated boundary; document it. E2E must exercise actual participating services.

NSubstitute and the generators can use reflection/dynamic proxies in the test process. This
does not add reflection to published service images or demonstrate application NativeAOT support.

## Data generation

- AutoFixture builds object graphs and fills incidental inputs. Freeze or Inject a dependency
  when the SUT and assertions must share its identity. Prefer explicit builders/customizations
  for domain invariants and constructors; do not bypass domain creation rules.
- Bogus supplies realistic names, addresses and DTO data through explicit rules.
- AutoBogus fills DTO members automatically and supports Bogus RuleFor overrides. Use a local
  seed, for example new AutoFaker<MyDto>().UseSeed(873), so failures can be reproduced.
- Its NSubstitute binder supports generating interface/abstract dependencies. Set required
  behavior explicitly rather than relying on automatically generated mock responses.
- Choose one generator for a given fixture. Do not stack AutoFixture and AutoBogus over the
  same graph. Avoid global Faker configuration or Randomizer.Seed in parallel test suites.
- Tenant IDs, permissions, prices, timestamps, ordering and boundary values relevant to an
  assertion remain explicit. Generated filler is not a substitute for edge-case tests.
- Give integration tests independent tenant/database/correlation identities; repeating a seed
  alone does not isolate parallel tests. Never generate real credentials or send fake users
  to production services. Seeded output may change after dependency upgrades.

FoundationTests exercises AutoFixture against connection-string escaping and seeded AutoBogus
against credential redaction. EventSerializationTests uses locally seeded Bogus metadata.
TenantConnectionTests, GrpcTests and IngressTests use NSubstitute at interface boundaries
and verify relevant calls while retaining the actual application pipeline.

Preserve assertion semantics when modifying tests: use explicit case sensitivity for strings,
assert exact exception types when the contract requires them, and parenthesize nullable
expressions so null-conditional access cannot skip an assertion.

## Endpoint and architecture tests

ApiTests uses FastEndpoints.Testing AppFixture/TestBase with Shouldly while retaining explicit
public URLs to verify route contracts. The fixture uses DisableWafCache to keep its lifecycle
isolated from independently configured hosts. For service fixtures, start Testcontainers in
PreSetupAsync and dispose owned resources in the matching fixture lifecycle. Host reuse is
allowed only when test state and container ownership are isolated correctly.

Typed FastEndpoints HTTP helpers are useful for ordinary endpoint tests. Keep explicit URL
checks for externally consumed contracts. Factory.Create endpoint unit tests do not execute the
full HTTP pipeline. FastEndpoints command/event receivers observe its own bus, not Wolverine.

The initial architecture suite checks Core cannot depend on Persistence/Infrastructure and
Persistence cannot depend on Infrastructure. Add service-specific slice, read/write handler,
contract and domain rules when those consumers exist; no empty-service architecture claims.

## Coverage

Coverage is installed and verified, but collecting it remains explicit. For example:

```sh
dotnet run --project tests/unit/SharedKernel.Core.UnitTests -c Release -- --coverlet --coverlet-output-format cobertura --coverlet-include "[SharedKernel.Core]*" --results-directory .artifacts/coverage/core
```

Use a separate results directory/prefix per suite to avoid overwrites. CI already uploads
.artifacts and TestResults. Include the same assemblies, generated-code policy and test layers
when comparing before/after coverage. Do not invent a non-regression claim from a single
snapshot. The [coverage ratchet](coverage.md) now merges full unit/integration
coverage and rejects missing assemblies, lower measurements and lowered baselines.
Use its canonical commands and policy rather than an ad hoc single-suite comparison.

## References

- [FastEndpoints testing](https://fast-endpoints.com/docs/integration-unit-testing)
- [NSubstitute usage and analyzer guidance](https://nsubstitute.github.io/help/getting-started/)
- [AutoFixture](https://github.com/AutoFixture/AutoFixture)
- [Bogus](https://github.com/bchavez/Faker)
- [AutoBogus](https://github.com/soenneker/soenneker.utils.autobogus)
- [AutoBogus NSubstitute binder](https://github.com/soenneker/soenneker.utils.autobogus.nsubstitute)
- [Coverlet for MTP](https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/Coverlet.MTP.Integration.md)
