import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

const descriptor = JSON.parse(
  readFileSync(
    new URL("../../../aspire/Teck.Cloud.AppHost/Identity/runtime.json", import.meta.url),
    "utf8",
  ),
);

export function imageBuildCommand(runtime = "docker", identity = descriptor) {
  if (runtime !== "docker" && runtime !== "podman") {
    throw new Error("ASPIRE_CONTAINER_RUNTIME must be docker or podman.");
  }
  return {
    command: runtime,
    args: ["build", "-f", identity.dockerfile, "-t", identity.image, "."],
  };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const { command, args } = imageBuildCommand(process.env.ASPIRE_CONTAINER_RUNTIME);
    const result = spawnSync(command, args, {
      cwd: fileURLToPath(new URL("../../../../", import.meta.url)),
      stdio: "inherit",
      shell: false,
    });
    if (result.error) throw result.error;
    process.exitCode = result.status ?? 1;
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
  }
}
