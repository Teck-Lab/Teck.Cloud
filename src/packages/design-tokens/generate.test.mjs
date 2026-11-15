import assert from "node:assert/strict";
import fs from "node:fs";
import test from "node:test";
import { renderTokens, validateTokens } from "./generate.mjs";
import { colors, colorTokens } from "./index.js";

const source = JSON.parse(fs.readFileSync(new URL("./tokens.json", import.meta.url), "utf8"));

test("one source generates the checked-in CSS and JavaScript palette", () => {
  const outputs = renderTokens(source);
  for (const [name, expected] of Object.entries(outputs)) {
    assert.equal(fs.readFileSync(new URL(`./${name}`, import.meta.url), "utf8"), expected);
  }
  assert.deepEqual(colorTokens, Object.keys(source.light));
  for (const mode of ["light", "dark"]) {
    for (const token of colorTokens) {
      assert.equal(colors[mode][token], `hsl(${source[mode][token].split(" ").join(", ")})`);
    }
  }
});

test("a palette change updates CSS and native color exports together", () => {
  const changed = structuredClone(source);
  changed.light.primary = "217 91% 60%";
  const outputs = renderTokens(changed);
  assert.match(outputs["tokens.css"], /--primary: 217 91% 60%/u);
  assert.match(outputs["index.js"], /primary: "hsl\(217, 91%, 60%\)"/u);
  assert.notEqual(
    outputs["tokens.css"],
    fs.readFileSync(new URL("./tokens.css", import.meta.url), "utf8"),
  );
});

test("rejects missing dark tokens and malformed colors", () => {
  const incomplete = structuredClone(source);
  delete incomplete.dark.primary;
  assert.throws(() => validateTokens(incomplete), /identical ordered color tokens/u);
  const invalid = structuredClone(source);
  invalid.light.primary = "999 100% 50%";
  assert.throws(() => validateTokens(invalid), /out of range/u);
});
