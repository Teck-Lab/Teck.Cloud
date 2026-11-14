# Documentation and instruction maintenance

- Keep one canonical document per responsibility; scoped AGENTS.md files give the
  small set of invariants and links needed at that boundary, not copies of manuals.
- Read [the instruction map](agent-instructions.md) when adding or moving guidance.
  Paths in AGENTS.md links are relative to that file. Verify all local links.
- Keep documented behavior aligned with implementation/tests. Distinguish current
  foundations, future service requirements and unverified external configuration.
- Document important limitations and exceptions where they apply. Do not present
  templates, package pins, readiness or source inspection as runtime evidence.
- Read only the documents relevant to the change. Update other pages when a known
  conflicting statement would send implementers down the wrong path.
- Versions belong in actual configuration and focused tooling docs; avoid copying
  version lists into every instruction file. Never include secrets or machine-
  specific private configuration.
- Do not duplicate host/plugin orchestration policy inside this repository.
