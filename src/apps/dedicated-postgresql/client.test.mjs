import assert from "node:assert/strict";
import test from "node:test";
import { readFile } from "node:fs/promises";
import ts from "typescript";

const source = await readFile(new URL("./src/api.ts", import.meta.url), "utf8");
const javascript = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext } }).outputText;
const api = await import(`data:text/javascript;base64,${Buffer.from(javascript).toString("base64")}`);

test("API and realtime client behavior", async () => {
  assert.equal(api.newerRevision("1", "2"), true);
  assert.equal(api.newerRevision("9007199254740992", "9007199254740993"), true);
});
