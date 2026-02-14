# Container Tooling

## Intent

Use Podman as the local container runtime preference, while keeping CI compatibility.

## Rules

- Local development examples should use `podman` commands.
- CI workflows may continue to use Docker when runner/tooling requires it.
- Clearly distinguish local-vs-CI command examples in documentation.
- Prefer rootless and least-privilege container execution when available.
