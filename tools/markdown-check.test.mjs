import assert from "node:assert/strict";
import { mkdir, mkdtemp, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { verifyLinks } from "./markdown-check.mjs";

async function inFixture(run) {
  const root = await mkdtemp(path.join(os.tmpdir(), "markdown-check-"));
  try {
    await run(root);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
}

test("accepts local files, README directories, fragments and ignored code examples", async () => {
  await inFixture(async (root) => {
    const docs = path.join(root, "docs");
    const guide = path.join(docs, "guide");
    await mkdir(guide, { recursive: true });
    const index = path.join(guide, "README.md");
    const source = path.join(docs, "start.md");
    await writeFile(index, "# Quick start\n\n## API & setup\n");
    await writeFile(
      source,
      [
        "# Start",
        "[guide](guide/)",
        "[section](guide/README.md#api--setup)",
        "```md",
        "[not checked](missing.md#nope)",
        "```",
        "",
      ].join("\n"),
    );
    assert.deepEqual(await verifyLinks([source, index], root), []);
  });
});

test("rejects missing local pages and missing fragments with source location", async () => {
  await inFixture(async (root) => {
    const source = path.join(root, "README.md");
    const target = path.join(root, "target.md");
    await writeFile(source, "# Home\n[missing](absent.md)\n[bad anchor](target.md#not-there)\n");
    await writeFile(target, "# Exists\n");
    const errors = await verifyLinks([source, target], root);
    assert.equal(errors.length, 2);
    assert.match(errors[0], /README\.md: broken local link absent\.md/);
    assert.match(errors[1], /README\.md:3: missing fragment #not-there in target\.md/);
  });
});

test("detects inbound references to renamed or deleted documentation pages", async () => {
  await inFixture(async (root) => {
    const readme = path.join(root, "README.md");
    const renamedPage = path.join(root, "old-name.md");
    await writeFile(readme, "# Home\n[former page](old-name.md#details)\n");
    await writeFile(renamedPage, "# New name\n\n## Details\n");
    assert.deepEqual(await verifyLinks([readme, renamedPage], root), []);

    await rm(renamedPage);
    const errors = await verifyLinks([readme], root);
    assert.equal(errors.length, 1);
    assert.match(errors[0], /README\.md: broken local link old-name\.md#details/);
  });
});

test("rejects unknown fragments on local instructions and custom anchors", async () => {
  await inFixture(async (root) => {
    const source = path.join(root, "README.md");
    const instructions = path.join(root, "instructions.md");
    await writeFile(
      source,
      "[anchor](instructions.md#configured-id)\n[bad](instructions.md#wrong)\n",
    );
    await writeFile(instructions, '<a id="configured-id"></a>\n# Heading\n');
    const errors = await verifyLinks([source, instructions], root);
    assert.equal(errors.length, 1);
    assert.match(errors[0], /missing fragment #wrong/);
  });
});
