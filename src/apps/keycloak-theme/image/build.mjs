import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import { lstatSync, readdirSync, readFileSync, readlinkSync } from "node:fs";
import { join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const repositoryRoot = fileURLToPath(new URL("../../../../", import.meta.url));
const labelName = "org.teck.cloud.source-sha256";
const generatedDirectories = new Set([
  ".artifacts",
  ".nx",
  "bin",
  "coverage",
  "dist",
  "dist_keycloak",
  "node_modules",
  "obj",
]);
const descriptor = JSON.parse(
  readFileSync(
    new URL("../../../aspire/Teck.Cloud.AppHost/Identity/runtime.json", import.meta.url),
    "utf8",
  ),
);

export function fingerprintTree(root, paths) {
  const hash = createHash("sha256");
  const visit = (path) => {
    const stat = lstatSync(path);
    if (stat.isDirectory()) {
      for (const entry of readdirSync(path, { withFileTypes: true }).sort((a, b) =>
        a.name.localeCompare(b.name, "en"),
      )) {
        if (!generatedDirectories.has(entry.name)) visit(join(path, entry.name));
      }
      return;
    }
    hash.update(relative(root, path).replaceAll("\\", "/"));
    hash.update("\0");
    hash.update(stat.isSymbolicLink() ? readlinkSync(path) : readFileSync(path));
    hash.update("\0");
  };
  for (const path of [...paths].sort()) visit(join(root, path));
  return hash.digest("hex");
}

export function imageSourceFingerprint(root = repositoryRoot) {
  return fingerprintTree(root, [
    ".dockerignore",
    "bun.lock",
    "package.json",
    "src/apps/keycloak-theme",
    "src/aspire/Teck.Cloud.AppHost/Identity/runtime.json",
    "src/packages/design-tokens",
    "tsconfig.base.json",
  ]);
}

export function imageBuildCommand(runtime = "docker", identity = descriptor, fingerprint) {
  if (runtime !== "docker" && runtime !== "podman") {
    throw new Error("ASPIRE_CONTAINER_RUNTIME must be docker or podman.");
  }
  return {
    command: runtime,
    args: [
      "build",
      "-f",
      identity.dockerfile,
      "-t",
      identity.image,
      ...(fingerprint ? ["--label", `${labelName}=${fingerprint}`] : []),
      ".",
    ],
  };
}

function installedFingerprint(runtime, image) {
  const result = spawnSync(runtime, ["image", "inspect", image], {
    cwd: repositoryRoot,
    encoding: "utf8",
    shell: false,
  });
  if (result.status !== 0) return undefined;
  const images = JSON.parse(result.stdout);
  const installed = Array.isArray(images) ? images[0] : images;
  return installed?.Config?.Labels?.[labelName] ?? installed?.Labels?.[labelName];
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const options = process.argv.slice(2);
    if (options.some((option) => option !== "--if-changed")) {
      throw new Error("Only --if-changed is supported.");
    }
    const runtime = process.env.ASPIRE_CONTAINER_RUNTIME;
    const fingerprint = imageSourceFingerprint();
    const { command, args } = imageBuildCommand(runtime, descriptor, fingerprint);
    if (
      options.includes("--if-changed") &&
      installedFingerprint(command, descriptor.image) === fingerprint
    ) {
      console.log(`Keycloak image ${descriptor.image} matches source; build skipped.`);
    } else {
      const result = spawnSync(command, args, {
        cwd: repositoryRoot,
        stdio: "inherit",
        shell: false,
      });
      if (result.error) throw result.error;
      process.exitCode = result.status ?? 1;
    }
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
  }
}
