import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const packageDirectory = path.dirname(fileURLToPath(import.meta.url));
const colorPattern = /^(\d+(?:\.\d+)?) (\d+(?:\.\d+)?)% (\d+(?:\.\d+)?)%$/u;

function validateTokens(source) {
  if (!source || typeof source !== "object" || Array.isArray(source)) {
    throw new Error("Token source must be an object.");
  }
  if (!/^\d+(?:\.\d+)?rem$/u.test(source.radius ?? "")) {
    throw new Error("radius must use rem units.");
  }
  const light = source.light;
  const dark = source.dark;
  if (!light || !dark || typeof light !== "object" || typeof dark !== "object") {
    throw new Error("Both light and dark palettes are required.");
  }
  const names = Object.keys(light);
  if (names.length === 0 || names.some((name) => !/^[a-z][a-z0-9-]*$/u.test(name))) {
    throw new Error("Color token names must be lowercase kebab case.");
  }
  if (names.join("|") !== Object.keys(dark).join("|")) {
    throw new Error("Light and dark palettes must have identical ordered color tokens.");
  }
  for (const mode of [light, dark]) {
    for (const name of names) {
      const value = mode[name];
      const match = typeof value === "string" ? colorPattern.exec(value) : null;
      if (!match) throw new Error(`Invalid HSL value for ${name}.`);
      const [hue, saturation, lightness] = match.slice(1).map(Number);
      if (hue > 360 || saturation > 100 || lightness > 100) {
        throw new Error(`HSL component out of range for ${name}.`);
      }
    }
  }
  return names;
}

function renderTokens(source) {
  const names = validateTokens(source);
  const css = [
    "/* Generated from tokens.json. Edit the source, then run node generate.mjs. */",
    ":root {",
    ...names.map((name) => `  --${name}: ${source.light[name]};`),
    `  --radius: ${source.radius};`,
    "}",
    "",
    ".dark {",
    ...names.map((name) => `  --${name}: ${source.dark[name]};`),
    "}",
    "",
  ].join("\n");
  const paletteLines = (mode) => [
    `  ${mode}: Object.freeze({`,
    ...names.map((name) => {
      const hsl = `hsl(${source[mode][name].split(" ").join(", ")})`;
      const key = /^[a-z][a-z0-9]*$/u.test(name) ? name : JSON.stringify(name);
      return `    ${key}: ${JSON.stringify(hsl)},`;
    }),
    "  }),",
  ];
  const js = [
    "// Generated from tokens.json. Edit the source, then run node generate.mjs.",
    "export const colorTokens = Object.freeze([",
    ...names.map((name) => `  ${JSON.stringify(name)},`),
    "]);",
    "",
    "export const colors = Object.freeze({",
    ...paletteLines("light"),
    ...paletteLines("dark"),
    "});",
    "",
  ].join("\n");
  const declarations = [
    "// Generated from tokens.json. Edit the source, then run node generate.mjs.",
    "export type ColorToken =",
    ...names.map(
      (name, index) => `  | ${JSON.stringify(name)}${index === names.length - 1 ? ";" : ""}`,
    ),
    'export type ColorScheme = "light" | "dark";',
    "export declare const colorTokens: readonly ColorToken[];",
    "export declare const colors: Readonly<Record<ColorScheme, Readonly<Record<ColorToken, string>>>>;",
    "",
  ].join("\n");
  return {
    "tokens.css": css,
    "tokens.css.d.ts": "// CSS side-effect entry point for TypeScript consumers.\nexport {};\n",
    "index.js": js,
    "index.d.ts": declarations,
  };
}

function main() {
  const source = JSON.parse(fs.readFileSync(path.join(packageDirectory, "tokens.json"), "utf8"));
  const outputs = renderTokens(source);
  const check = process.argv.includes("--check");
  for (const [name, expected] of Object.entries(outputs)) {
    const destination = path.join(packageDirectory, name);
    if (check) {
      if (!fs.existsSync(destination) || fs.readFileSync(destination, "utf8") !== expected) {
        throw new Error(`${name} is out of date. Run node generate.mjs.`);
      }
    } else {
      fs.writeFileSync(destination, expected);
    }
  }
  console.log(check ? "Design tokens are current." : "Design tokens generated.");
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    main();
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
  }
}

export { renderTokens, validateTokens };
