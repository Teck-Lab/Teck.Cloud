Integration tests: running with Podman (local) vs Docker (CI)

This project uses Testcontainers-dotnet to start Postgres and RabbitMQ for integration tests.

Podman (local developer machines)
- Podman Desktop can be used locally, but Testcontainers (via Docker.DotNet) requires a Docker-compatible socket.
- Podman Desktop provides an option to expose a Docker-compatible unix socket. Follow the guide:
  https://podman-desktop.io/tutorial/testcontainers-with-podman
- After enabling the socket, set the environment variable (example):
  - Windows (PowerShell): $env:TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE = "unix:///run/podman/podman.sock"
  - Linux/macOS (bash): export TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=unix:///run/podman/podman.sock
- Alternatively set DOCKER_HOST to the unix socket (e.g. unix:///run/podman/podman.sock).

CI (use Docker)
- On CI runners you should use Docker (Docker Desktop, Docker on Linux, or the platform-provided Docker service in your CI).
- Ensure Docker is available and the runner user can connect to Docker.

Notes
- The test fixture attempts to detect common unix socket paths and will set `TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE` automatically when it finds a socket.
- If your environment uses Podman's named pipe path on Windows (e.g. `npipe:////./pipe/podman_engine`), Docker.DotNet may not accept it and you'll need to configure Podman Desktop to expose a unix socket.
- If you have trouble, run tests with verbose logs and check the console output from the test fixture for diagnostic lines beginning with `[Testcontainers]`.
