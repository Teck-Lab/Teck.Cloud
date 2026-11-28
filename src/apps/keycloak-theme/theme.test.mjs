import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import test from "node:test";
import { fingerprintTree, imageBuildCommand, imageSourceFingerprint } from "./image/build.mjs";
import {
  accountControlLabels,
  accountLanguageLinks,
  accountProfileFields,
} from "./src/account-presentation.ts";
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
test("image cache fingerprint changes with source but ignores generated outputs", () => {
  const root = mkdtempSync(join(tmpdir(), "teck-theme-fingerprint-"));
  try {
    mkdirSync(join(root, "source"));
    writeFileSync(join(root, "source", "page.tsx"), "original");
    const initial = fingerprintTree(root, ["source"]);
    mkdirSync(join(root, "source", "dist_keycloak"));
    writeFileSync(join(root, "source", "dist_keycloak", "theme.jar"), "generated");
    assert.equal(fingerprintTree(root, ["source"]), initial);
    writeFileSync(join(root, "source", "page.tsx"), "changed");
    assert.notEqual(fingerprintTree(root, ["source"]), initial);
    const build = imageBuildCommand("podman", runtime, initial);
    assert.deepEqual(build.args.slice(-3, -1), [
      "--label",
      `org.teck.cloud.source-sha256=${initial}`,
    ]);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("Keycloak image fingerprint changes for a preset-only edit", () => {
  const root = mkdtempSync(join(tmpdir(), "teck-theme-preset-fingerprint-"));
  try {
    for (const path of [
      ".dockerignore",
      "bun.lock",
      "package.json",
      "tsconfig.base.json",
      "src/apps/keycloak-theme/theme.css",
      "src/aspire/Teck.Cloud.AppHost/Identity/runtime.json",
      "src/packages/design-tokens/tokens.json",
      "src/packages/tailwind-config/keycloak.mjs",
    ]) {
      const file = join(root, path);
      mkdirSync(dirname(file), { recursive: true });
      writeFileSync(file, "placeholder");
    }
    const preset = join(root, "src/packages/tailwind-config/keycloak.mjs");
    writeFileSync(preset, 'export default { darkMode: "class" };\n');
    const initial = imageSourceFingerprint(root);
    writeFileSync(preset, 'export default { darkMode: "media" };\n');
    assert.notEqual(imageSourceFingerprint(root), initial);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
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

test("account languages follow realm-provided links and hide unavailable choices", () => {
  const supported = [
    { languageTag: "en", label: "English", url: "/realms/teck-local/account/?kc_locale=en" },
    { languageTag: "da", label: "Dansk", url: "/realms/teck-local/account/?kc_locale=da" },
  ];
  assert.deepEqual(accountLanguageLinks(undefined), []);
  assert.deepEqual(accountLanguageLinks({ supported: supported.slice(0, 1) }), []);
  assert.deepEqual(accountLanguageLinks({ supported }), supported);
  assert.deepEqual(accountLanguageLinks({ supported }, false), []);
});

test("local realm enables the account theme locales", () => {
  const realm = JSON.parse(
    readFileSync(
      new URL(
        "../../aspire/Teck.Cloud.AppHost/Identity/Realms/teck-local-realm.json",
        import.meta.url,
      ),
      "utf8",
    ),
  );
  assert.equal(realm.internationalizationEnabled, true);
  assert.equal(realm.defaultLocale, "en");
  assert.deepEqual(realm.supportedLocales, ["en", "da", "tr"]);
});

test("account profile exposes only declared fields, never Keycloak internal attributes", () => {
  const values = { nickname: ["Pat"], is_temporary_admin: ["true"] };
  assert.deepEqual(accountProfileFields({ attributes: values }), []);
  const fields = accountProfileFields({
    attributes: values,
    userProfileMetadata: {
      attributes: [
        { name: "nickname", displayName: "Nickname" },
        { name: "is_temporary_admin", displayName: "is_temporary_admin" },
        { name: "email", displayName: "Email" },
      ],
    },
  });
  assert.deepEqual(
    fields.map(({ name }) => name),
    ["nickname"],
  );
  assert.deepEqual(fields[0].value, ["Pat"]);
});

test("account appearance controls follow the selected language with a safe fallback", () => {
  assert.equal(accountControlLabels("en").themeLight, "Light");
  assert.equal(accountControlLabels("da").themeDark, "Mørk");
  assert.equal(accountControlLabels("tr").themeSystem, "Sistem");
  assert.equal(accountControlLabels("fr").themeLight, "Light");
});
