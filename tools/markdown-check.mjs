import { spawnSync } from "node:child_process";
import { readdir, readFile, stat } from "node:fs/promises";
import { createRequire } from "node:module";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const require = createRequire(import.meta.url);
const ignoredDirectories = new Set([
  ".git",
  "node_modules",
  ".nx",
  ".artifacts",
  "dist",
  "coverage",
  "bin",
  "obj",
]);

function isMarkdown(file) {
  return /\.md(?:own)?$/i.test(file);
}

async function collectMarkdown(directory, files = []) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    if (entry.isDirectory() && ignoredDirectories.has(entry.name)) continue;
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) await collectMarkdown(fullPath, files);
    else if (entry.isFile() && isMarkdown(entry.name)) files.push(fullPath);
  }
  return files;
}

function markdownBody(text) {
  return text
    .replace(/^\s*(```|~~~)[^\n]*\n[\s\S]*?^\s*\1[^\n]*(?:\n|$)/gm, "")
    .replace(/<!--[\s\S]*?-->/g, "");
}

function slugify(heading) {
  return heading
    .replace(/<[^>]*>/g, "")
    .replace(/`([^`]*)`/g, "$1")
    .replace(/\\([\\`*_{}[\]()#+.!<>|])/g, "$1")
    .toLowerCase()
    .replace(/[^\p{L}\p{N}\p{M}\-_\s]/gu, "")
    .trim()
    .replace(/\s/g, "-");
}

function anchorsFor(text) {
  const anchors = new Set();
  const counts = new Map();
  for (const line of markdownBody(text).split(/\r?\n/)) {
    const heading = line.match(/^\s{0,3}#{1,6}\s+(.+?)\s*#*\s*$/)?.[1];
    if (heading) {
      const base = slugify(heading);
      const count = counts.get(base) ?? 0;
      counts.set(base, count + 1);
      anchors.add(count ? `${base}-${count}` : base);
    }
    for (const match of line.matchAll(/<a\s+(?:[^>]*?\s)?(?:id|name)=["']([^"']+)["']/gi))
      anchors.add(match[1]);
  }
  return anchors;
}

function localLinks(text) {
  const body = markdownBody(text);
  const links = [];
  for (const match of body.matchAll(
    /!?\[[^\]]*\]\(\s*(?:<([^>]+)>|([^\s)]+))(?:\s+["'][^)]*["'])?\s*\)/g,
  )) {
    links.push({ destination: match[1] ?? match[2], offset: match.index });
  }
  for (const match of body.matchAll(/^\s*<(?!https?:|mailto:|data:)([^>]+)>\s*$/gm)) {
    if (!match[1].includes(" ")) links.push({ destination: match[1], offset: match.index });
  }
  return links;
}

export async function verifyLinks(files, base = root) {
  const contents = new Map();
  for (const file of files) contents.set(file, await readFile(file, "utf8"));
  const errors = [];
  for (const [source, text] of contents) {
    const relativeSource = path.relative(base, source).split(path.sep).join("/");
    for (const { destination, offset } of localLinks(text)) {
      let decoded;
      try {
        decoded = decodeURIComponent(destination);
      } catch {
        errors.push(`${relativeSource}: invalid percent encoding in link ${destination}`);
        continue;
      }
      if (/^(?:[a-z][a-z\d+.-]*:|\/\/)/i.test(decoded)) continue;
      const hash = decoded.indexOf("#");
      const destinationPath = hash < 0 ? decoded : decoded.slice(0, hash);
      const pathname = destinationPath.split("?")[0];
      const fragment = hash < 0 ? "" : decoded.slice(hash + 1);
      let target = pathname ? path.resolve(path.dirname(source), pathname) : source;
      try {
        const targetStat = await stat(target);
        if (targetStat.isDirectory()) {
          const index = path.join(target, "README.md");
          await stat(index);
          target = index;
        }
        if (!targetStat.isDirectory() && !targetStat.isFile()) {
          errors.push(`${relativeSource}: link target is not a file: ${decoded}`);
          continue;
        }
      } catch {
        errors.push(`${relativeSource}: broken local link ${decoded}`);
        continue;
      }
      if (!fragment) continue;
      if (!isMarkdown(target)) continue;
      const targetText = contents.get(target) ?? (await readFile(target, "utf8"));
      if (!anchorsFor(targetText).has(fragment)) {
        const line = text.slice(0, offset).split(/\r?\n/).length;
        errors.push(
          `${relativeSource}:${line}: missing fragment #${fragment} in ${path.relative(base, target).split(path.sep).join("/")}`,
        );
      }
    }
  }
  return errors;
}
function runMarkdownLint(files) {
  let entry;
  try {
    entry = require.resolve("markdownlint-cli2");
  } catch {
    return ["markdownlint-cli2 is not installed; run bun install"];
  }
  const result = spawnSync(
    process.execPath,
    [entry, ...files.map((file) => path.relative(root, file))],
    {
      cwd: root,
      encoding: "utf8",
      stdio: "inherit",
      windowsHide: true,
    },
  );
  if (result.error) return [`could not run markdownlint-cli2: ${result.error.message}`];
  return result.status === 0
    ? []
    : [`markdownlint-cli2 exited with status ${result.status ?? "unknown"}`];
}

export async function checkMarkdown({ lint = true } = {}) {
  const files = (await collectMarkdown(root)).sort();
  const errors = [...(lint ? runMarkdownLint(files) : []), ...(await verifyLinks(files))];
  return {
    files: files.map((file) => path.relative(root, file).split(path.sep).join("/")),
    errors,
  };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const result = await checkMarkdown();
  if (result.errors.length) {
    for (const error of result.errors) console.error(error);
    process.exitCode = 1;
  } else {
    console.log(`Markdown lint and local links passed (${result.files.length} files).`);
  }
}
