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
| Mocks and interaction checks                 | NSubstitute 6.2.0                                              | Core/Infrastructure unit tests                                      |
| Test data and object generation              | AutoFixture 5.0.0, AutoBogus 2.0.1                             | Core/Infrastructure unit tests                                      |
| Containerized integration environments       | Testcontainers 4.11.0                                           | Integration tests                                                   |
| Application host integration                 | Aspire.Hosting.Testing 13.2.0                                   | AppHost tests                                                       |
| Test runner                                  | xunit.v3 3.2.2, xunit.runner.visualstudio 3.1.5                | All executable test projects                                        |
| Microsoft Testing Platform                   | Microsoft.Testing.Platform 2.1.0                                | All executable test projects                                        |
| Test SDK                                     | Microsoft.NET.Test.Sdk 18.0.1                                  | All executable test projects                                        |
| Coverage                                      | coverlet.MTP 8.0.2                                              | Test coverage                                                       |
| Test report generation                       | Microsoft.Testing.Extensions.TrxReport 2.1.0                   | All executable test projects                                        |
| Fake data                                    | Bogus 35.6.3                                                    | Core/Infrastructure unit tests                                      |

## Generated data

Prefer AutoFixture with AutoBogus for randomized domain data. Use deterministic
seeds where test reproducibility matters.

## Test conventions

- Keep tests close to production projects under `tests/`.
- Unit tests must be fast and isolated.
- Integration tests may use Testcontainers and Aspire resources where appropriate.
- Use Shouldly for assertions.
- Use NSubstitute for dependency isolation and interaction verification.
- Avoid excessive mocking of implementation details.

## Running tests

Use the `dotnet test` commands documented in [test layers](testing.md#test-layers).
For repository tooling, use the standalone `bun run test:tooling` lane described in
[`docs/testing.md`](testing.md#test-layers).