import assert from "node:assert/strict";
import { readdirSync, readFileSync } from "node:fs";
import test from "node:test";
import { imageBuildCommand } from "./image/build.mjs";
import { useThemeConfig } from "./src/branding.ts";
import { parseRealmLogoConfig } from "./src/vendor/keycloak-theme/lib/realmLogoConfig.ts";
import { hexToHslTuple } from "./src/vendor/keycloak-theme/lib/themeColors.ts";

test("static branding does not consult network or browser storage", () => {
  assert.deepEqual(useThemeConfig("tenant-a"), {});
  assert.deepEqual(useThemeConfig("tenant-b"), {});
});
test("theme colors reject malformed input and convert black/white/red", () => {
  assert.equal(hexToHslTuple("invalid"), null);
  assert.equal(hexToHslTuple("#000000"), "0 0% 0%");
  assert.equal(hexToHslTuple("#ffffff"), "0 0% 100%");
  assert.equal(hexToHslTuple("#ff0000"), "0 100% 50%");
});
test("realm logo parsing is safe with absent or empty values", () => {
  assert.deepEqual(parseRealmLogoConfig(undefined), {});
  assert.deepEqual(parseRealmLogoConfig("<!--logo-light:   -->"), {});
});
test("production bundle omits the uninstalled theme-config SPI request", () => {
  const assets = new URL("./dist/assets/", import.meta.url);
  const scripts = readdirSync(assets).filter((name) => name.endsWith(".js"));
  assert.ok(scripts.length > 0);
  for (const name of scripts)
    assert.doesNotMatch(readFileSync(new URL(name, assets), "utf8"), /theme-config/);
});

test("image builder follows Aspire's Docker or Podman runtime", () => {
  assert.equal(imageBuildCommand().command, "docker");
  const docker = imageBuildCommand("docker");
  const podman = imageBuildCommand("podman");
  assert.equal(podman.command, "podman");
  assert.deepEqual(podman.args, docker.args);
  assert.equal(podman.args[podman.args.indexOf("-t") + 1], runtime.image);
});
test("image builder rejects an arbitrary executable or shell command", () => {
  for (const runtime of ["", "sh", "docker; echo injected", "../podman"]) {
    assert.throws(() => imageBuildCommand(runtime), /must be docker or podman/);
  }
});

const runtime = JSON.parse(
  readFileSync(
    new URL("../../aspire/Teck.Cloud.AppHost/Identity/runtime.json", import.meta.url),
    "utf8",
  ),
);

test("image build consumes the shared descriptor candidate instead of a duplicated tag", () => {
  const candidate = { ...runtime, image: "teck/keycloak:test-candidate" };
  const build = imageBuildCommand("podman", candidate);
  assert.equal(build.args[build.args.indexOf("-t") + 1], candidate.image);
  assert.equal(build.args[build.args.indexOf("-f") + 1], candidate.dockerfile);
});

test("optimized and final Keycloak stages use the descriptor's exact digest-pinned base", () => {
  const dockerfile = readFileSync(
    new URL(`../../../${runtime.dockerfile}`, import.meta.url),
    "utf8",
  );
  const bases = [...dockerfile.matchAll(/^FROM (quay\.io\/keycloak\/keycloak:\S+)/gm)].map(
    (match) => match[1],
  );
  assert.match(runtime.baseImage, /@sha256:[a-f0-9]{64}$/);
  assert.equal(bases.length, 2);
  assert.deepEqual(bases, [runtime.baseImage, runtime.baseImage]);
});
