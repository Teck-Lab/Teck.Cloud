import assert from "node:assert/strict";
import fs from "node:fs";
import { createRequire } from "node:module";
import os from "node:os";
import path from "node:path";
import test from "node:test";

const require = createRequire(import.meta.url);
const nx = path.dirname(require.resolve("nx/package.json"));
const { findMatchingProjects, getMatchingStringsWithCache } = require(
  path.join(nx, "dist/src/utils/find-matching-projects.js"),
);
const { FsTree } = require(path.join(nx, "dist/src/generators/tree.js"));
const { glob } = require(path.join(nx, "dist/src/generators/utils/glob.js"));
const commands = require(path.join(nx, "dist/src/command-line/release/utils/exec-command.js"));
const { RepoGitTags } = require(
  path.join(nx, "dist/src/command-line/release/utils/repository-git-tags.js"),
);

const projects = Object.fromEntries(
  [
    ["Core.UnitTests", "tests/unit/core", ["lane:unit"]],
    ["Persistence.IntegrationTests", "tests/integration/persistence", ["lane:integration"]],
    ["Api.Host", "src/services/api", ["type:host"]],
  ].map(([name, root, tags]) => [name, { name, data: { root, tags } }]),
);

test("Nx wildcard project selection preserves exclusions, tags and directories", () => {
  assert.deepEqual(findMatchingProjects(["*Tests", "!*.IntegrationTests"], projects), [
    "Core.UnitTests",
  ]);
  assert.deepEqual(
    findMatchingProjects(["tag:lane:*", "!directory:tests/integration/**"], projects),
    ["Core.UnitTests"],
  );
  assert.deepEqual(findMatchingProjects(["directory:src/**"], projects), ["Api.Host"]);
  assert.deepEqual(findMatchingProjects(["DoesNotExist*"], projects), []);
});

test("Nx cached matching preserves braces, extglobs, dots and negation", () => {
  const values = [".hidden.Tests", "Core.Tests", "Api.Host", "Ui.Tests"];
  assert.deepEqual(getMatchingStringsWithCache("*.Tests", values), [
    ".hidden.Tests",
    "Core.Tests",
    "Ui.Tests",
  ]);
  assert.deepEqual(getMatchingStringsWithCache("*.Tests", ["Api.Tests", "Api.Host"]), [
    "Api.Tests",
  ]);
  assert.deepEqual(getMatchingStringsWithCache("{Core,Ui}.Tests", values), [
    "Core.Tests",
    "Ui.Tests",
  ]);
  assert.deepEqual(getMatchingStringsWithCache("@(Core|Ui).Tests", values), [
    "Core.Tests",
    "Ui.Tests",
  ]);
  assert.deepEqual(getMatchingStringsWithCache("!*.Tests", values), ["Api.Host"]);
});

test("Nx generator globs include pending files and exclude deleted files", () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "teck-nx-glob-"));
  try {
    fs.writeFileSync(path.join(root, "nx.json"), "{}");
    fs.mkdirSync(path.join(root, "src"));
    fs.writeFileSync(path.join(root, "src/existing.cs"), "before");
    fs.writeFileSync(path.join(root, "src/deleted.cs"), "before");
    const tree = new FsTree(root, false);
    tree.write("src/created.ts", "created");
    tree.write("src/ignored.txt", "ignored");
    tree.write("src/existing.cs", "updated");
    tree.delete("src/deleted.cs");
    assert.deepEqual(glob(tree, ["src/**/*.{cs,ts}"]).sort(), [
      "src/created.ts",
      "src/existing.cs",
    ]);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("Nx release branch patterns choose all versus merged tags without remote Git", async (context) => {
  let branch = "release/1.0";
  const calls = [];
  context.mock.method(commands, "execCommand", async (command, args) => {
    assert.equal(command, "git");
    if (args[0] === "rev-parse") return branch;
    calls.push(args);
    return "v1.0.0\n";
  });
  for (const [current, patterns, all] of [
    ["release/1.0", ["release/*"], true],
    ["feature/api", ["release/*"], false],
    [".preview", ["*"], true],
    ["feature/api", ["!release/*"], true],
  ]) {
    branch = current;
    assert.deepEqual(await RepoGitTags.create().resolveTags(patterns), ["v1.0.0"]);
    assert.equal(calls.at(-1).includes("--merged"), !all);
  }
});

test("Nx cached matching retains invalid empty/comment pattern rejection", () => {
  assert.throws(() => getMatchingStringsWithCache("", [""]), /Invalid glob pattern/);
  assert.throws(
    () => getMatchingStringsWithCache("#ignored", ["#ignored"]),
    /Invalid glob pattern/,
  );
});
