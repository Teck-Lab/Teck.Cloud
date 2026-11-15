import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const runtime = process.env.ASPIRE_CONTAINER_RUNTIME ?? "docker";
if (runtime !== "docker" && runtime !== "podman") {
  throw new Error("ASPIRE_CONTAINER_RUNTIME must be docker or podman.");
}
const result = spawnSync(
  runtime,
  [
    "build",
    "-f",
    "tests/fixtures/Identity.TestHost/Dockerfile",
    "-t",
    "localhost/teck/identity-probe:local",
    ".",
  ],
  {
    cwd: fileURLToPath(new URL("../../../", import.meta.url)),
    stdio: "inherit",
    shell: false,
  },
);
if (result.error) throw result.error;
process.exitCode = result.status ?? 1;
