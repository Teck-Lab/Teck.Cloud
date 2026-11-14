# Unit tests

Read [test tooling](../../docs/test-tooling.md) for assertion/mock/data conventions.

- Test domain invariants, handler decisions and pure policies without infrastructure.
  Keep tests fast, independent and deterministic.
- Use Shouldly and NSubstitute at interface boundaries. Do not mock the class under
  test, DbContext/DbSet/IQueryable or non-virtual concrete members.
- AutoFixture constructs incidental graphs; Bogus/AutoBogus supplies seeded filler.
  Choose one generator per graph and set behavior-relevant values explicitly.
- Verify meaningful outcomes and interactions, including rejected/edge cases.
  Do not mirror private implementation details merely to increase coverage.
- Transaction, SQL, broker and HTTP-pipeline claims belong in integration tests.
  Factory.Create endpoint tests do not run the actual HTTP pipeline.
