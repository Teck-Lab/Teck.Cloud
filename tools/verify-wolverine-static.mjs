import { spawnSync } from "node:child_process";
import { mkdirSync, readdirSync, rmSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("../", import.meta.url));
const output = path.join(root, ".artifacts", "wolverine-generated");
const project = "tests/integration/SharedKernel.Infrastructure.IntegrationTests";
const environment = { ...process.env, TECK_CODEGEN_OUTPUT: output };

function run(args) {
  const result = spawnSync("dotnet", args, { cwd: root, env: environment, stdio: "inherit" });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`dotnet exited with ${result.status}`);
}

// Verify the fixed target before deleting only this script's generated output.
if (path.relative(root, output) !== path.join(".artifacts", "wolverine-generated")) {
  throw new Error("Generated output must stay inside the repository artifacts directory.");
}
rmSync(output, { recursive: true, force: true });
mkdirSync(output, { recursive: true });
try {
  run(["test", "--project", project]);
  const handlers = path.join(output, "WolverineHandlers");
  if (!readdirSync(handlers).some((name) => name.endsWith(".cs"))) {
    throw new Error("No generated handlers were produced.");
  }
  run(["test", "--project", project, "-p:VerifyStaticHandlers=true"]);
} finally {
  // Restore normal project assets for subsequent Nx builds.
  run(["restore", "Teck.Cloud.slnx"]);
}
