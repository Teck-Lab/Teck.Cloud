# NuGet update audit: 2026-11-13

The application target remains net10.0. All 99 existing central NuGet pins were checked
against the official NuGet version index; AutoFixture.AutoNSubstitute was then added as the
100th pin. Aspire.AppHost.Sdk was checked separately and remains current at 13.6.5.

Selection uses the newest stable release compatible with .NET 10. Existing preview-only
pins retain their preview channel; AutoFixture 5.0.0-rc.1 and its matching adapter are the
explicitly approved addition. Other alpha/dev/preview channels are not adopted automatically.
Nuspec framework groups were checked with NuGet.Frameworks' net10.0 compatibility provider,
then the actual dependency graph was restored and the solution compiled with warnings as errors.

## Updated pins

| Package                                         | Previous      | Selected   |
| ----------------------------------------------- | ------------- | ---------- |
| AutoFixture                                     | 4.18.1        | 5.0.0-rc.1 |
| Finbuckle.MultiTenant.EntityFrameworkCore       | 10.1.6        | 10.2.0     |
| Grpc.Net.ClientFactory                          | 2.84.0        | 2.85.0     |
| Meziantou.Analyzer                              | 3.0.338       | 3.0.339    |
| Microsoft.Extensions.Caching.StackExchangeRedis | 10.0.15       | 11.0.0     |
| Microsoft.Extensions.Configuration.Abstractions | 10.0.15       | 11.0.0     |
| Microsoft.Extensions.Diagnostics.HealthChecks   | 10.0.15       | 11.0.0     |
| Microsoft.Extensions.Hosting.Abstractions       | 10.0.15       | 11.0.0     |
| Microsoft.Extensions.Http.Resilience            | 10.0.0        | 11.0.0     |
| StackExchange.Redis                             | 3.3.1         | 3.4.35     |
| Testcontainers.Keycloak                         | 4.15.0        | 4.16.0     |
| Testcontainers.PostgreSql                       | 4.15.0        | 4.16.0     |
| Testcontainers.RabbitMq                         | 4.15.0        | 4.16.0     |
| Testcontainers.Redis                            | 4.15.0        | 4.16.0     |
| AutoFixture.AutoNSubstitute                     | Not installed | 5.0.0-rc.1 |

Microsoft.Extensions 11.0.0 packages listed above provide compatible assets for net10.0.
Their version number alone does not require retargeting the application to .NET 11.
Testcontainers modules are aligned to 4.16.0; AutoFixture packages are aligned to the RC.

## Newer versions excluded by the target

Microsoft.AspNetCore.Authentication.JwtBearer, Microsoft.AspNetCore.Mvc.Testing and the
three Microsoft.EntityFrameworkCore pins remain at 10.0.15. Their newer 11.0.0 packages
require .NET 11. Do not force them through compatibility warnings or change the application
framework merely to remove an outdated-package report entry.

All other central pins were already current under the policy above. This is a NuGet package
update; it does not upgrade the .NET SDK, Bun/Nx packages, container images or infrastructure
charts. Transitive versions are resolved by NuGet dependency constraints rather than overridden
individually to arbitrary newest releases.

The approved AutoNSubstitute adapter is referenced by the same test projects as AutoFixture;
TenantConnectionTests now exercises AutoNSubstituteCustomization. See [test tooling](test-tooling.md).

Sources: [NuGet v3 metadata](https://api.nuget.org/v3/index.json),
[framework compatibility](https://learn.microsoft.com/en-us/nuget/concepts/target-frameworks),
[AutoNSubstitute RC package dependencies](https://api.nuget.org/v3-flatcontainer/autofixture.autonsubstitute/5.0.0-rc.1/autofixture.autonsubstitute.nuspec).

## Verification

- Final metadata audit: 100 central pins, zero remaining compatible updates under this policy.
- Full Release solution build: zero warnings and errors.
- 29 unit, 2 architecture, 47 Testcontainers integration and 1 Aspire Sentinel E2E tests passed.
- Static Wolverine validation passed both normal and generated-handler runs (25 tests each).
- The .NET policy verifier, Biome check and Nx sync check passed.
- NuGet's direct/transitive vulnerability audit returned no known vulnerable packages.
- Equivalent Core unit coverage remained 48.1% lines and 44.2% branches; this is not a
  whole-solution coverage figure or an automated non-regression gate.

The failover run exposed a test startup race: replication could complete before Sentinel
had discovered a promotable replica. The test now waits for all three Sentinels to report
that replica and the other peers, with CKQUORUM succeeding, before stopping the primary.
The recovery assertions still require the original connection, retained data, shared cache
access and healthy readiness after real promotion.
