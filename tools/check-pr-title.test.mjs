import assert from "node:assert/strict";
import test from "node:test";
import { validateTitle } from "./check-pr-title.mjs";

test("accepts scoped, breaking and dependency update titles", async () => {
  for (const title of [
    "feat(billing): add invoice export",
    "fix!: reject invalid tenants",
    "chore(deps): bump the nuget group with 12 updates",
  ]) {
    assert.equal((await validateTitle(title)).valid, true, title);
  }
});

test("rejects empty, multiline, missing subject and nonconventional titles", async () => {
  for (const title of [
    "",
    "fix: ",
    "Update stuff",
    "fix: valid\n\nfeat: another header",
    "random: update",
  ]) {
    assert.equal((await validateTitle(title)).valid, false, title);
  }
});
