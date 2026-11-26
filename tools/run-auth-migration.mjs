import { spawn } from "node:child_process";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const applications = new Set(["storefront", "admin"]);

function readApplication(argv) {
  const index = argv.indexOf("--app");
  const name = index === -1 ? null : argv[index + 1];
  if (name === null || !applications.has(name)) {
    throw new Error(`Expected --app <${[...applications].join("|")}>.`);
  }
  return name;
}

function run() {
  const application = readApplication(process.argv.slice(2));
  const applicationDirectory = path.join(repositoryRoot, "src", "apps", application);
  const configPath = "./src/server/auth-cli.ts";

  try {
    readFileSync(path.join(applicationDirectory, "src", "server", "auth-cli.ts"), "utf8");
  } catch {
    throw new Error(`Missing ${application} Better Auth CLI configuration at ${configPath}.`);
  }

  const requireFromApplication = createRequire(path.join(applicationDirectory, "package.json"));
  // The auth package does not export its package.json, so locate the root from its public API subpath.
  const cliApiPath = requireFromApplication.resolve("auth/api");
  const cliPackageDirectory = path.dirname(path.dirname(cliApiPath));
  const cliPackage = JSON.parse(
    readFileSync(path.join(cliPackageDirectory, "package.json"), "utf8"),
  );
  const cliBinary = typeof cliPackage.bin === "string" ? cliPackage.bin : cliPackage.bin?.auth;
  if (typeof cliBinary !== "string") {
    throw new Error("The pinned auth CLI does not expose a binary.");
  }

  // The RSC condition resolves any server-only marker the config imports.
  const child = spawn(
    process.execPath,
    [
      "--conditions=react-server",
      path.resolve(cliPackageDirectory, cliBinary),
      "migrate",
      "--config",
      configPath,
      "--yes",
    ],
    { cwd: applicationDirectory, stdio: "inherit" },
  );

  child.on("error", (error) => {
    console.error(error.message);
    process.exitCode = 1;
  });

  child.on("exit", (code, signal) => {
    if (signal !== null) {
      console.error(`Better Auth migration stopped by ${signal}.`);
      process.exitCode = 1;
      return;
    }
    process.exitCode = code ?? 1;
  });
}

try {
  run();
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  process.exitCode = 1;
}
