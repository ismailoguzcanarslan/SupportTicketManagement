// Bundles each form / command entry point into a standalone browser script for upload to
// Dataverse as a JavaScript Web Resource.
//
// Convention (an entry is a file or a folder with an index.ts):
//   src/forms/<name>/index.ts (or src/forms/<name>.ts)  -> dist/webresources/forms/<name>.js     exposed as Ogz.Forms.<Name>
//   src/commands/<name>.ts (or src/commands/<name>/index.ts) -> dist/webresources/commands/<name>.js  exposed as Ogz.Commands.<Name>
//
// Each output file contains the entry point plus every module it imports (helpers, rules,
// crm-entities constants): the ES-module equivalent of `/// <reference path="...">`, but resolved at
// build time, so the Web Resource needs no module loader. Only the entry point's exports become
// public; everything else stays private inside the bundle's IIFE. The Xrm runtime is a global
// provided by Dataverse and is never bundled.
//
// Each bundle owns exactly one leaf of the global namespace, so several Web Resources can be
// loaded on the same form without overwriting each other. Event handlers are registered in
// Dataverse by their fully qualified name, e.g. "Ogz.Forms.Account.onLoad".
//
// Usage: node build.mjs [--dev] [--watch]

import { readdirSync, existsSync } from "node:fs";
import { join, basename, dirname } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import * as esbuild from "esbuild";

const ROOT_NAMESPACE = "Ogz";
const OUTPUT_DIR = join("dist", "webresources");
const ENTRY_GROUPS = [
  { dir: "forms", namespace: "Forms" },
  { dir: "commands", namespace: "Commands" },
];
const PACKAGE_DIR = dirname(fileURLToPath(import.meta.url));
// Bundle crm-entities from its TypeScript sources: esbuild then inlines the Choice enum values a
// form uses and drops the rest, instead of shipping every compiled enum of the table.
const ENTITIES_SOURCE = join(PACKAGE_DIR, "..", "crm-entities", "src", "index.ts");

export const toPascalCase = (name) =>
  name.replace(/(^|[-_.\s]+)([a-zA-Z0-9])/g, (_, __, c) => c.toUpperCase());

/** Entry points in sorted order, so output is the same on every machine. */
export function findEntries(packageDir = PACKAGE_DIR) {
  const entries = [];
  for (const group of ENTRY_GROUPS) {
    const dir = join(packageDir, "src", group.dir);
    if (!existsSync(dir)) continue;
    const found = new Map();
    for (const item of readdirSync(dir, { withFileTypes: true }).sort((a, b) => (a.name < b.name ? -1 : a.name > b.name ? 1 : 0))) {
      let name;
      let entryPoint;
      if (item.isDirectory() && existsSync(join(dir, item.name, "index.ts"))) {
        name = item.name;
        entryPoint = join(dir, item.name, "index.ts");
      } else if (item.isFile() && item.name.endsWith(".ts") && !item.name.endsWith(".d.ts") && !item.name.endsWith(".test.ts")) {
        name = basename(item.name, ".ts");
        entryPoint = join(dir, item.name);
      } else {
        continue;
      }
      // Both src/forms/x.ts and src/forms/x/index.ts would write the same bundle and global.
      if (found.has(name)) throw new Error(`crm-client: ${found.get(name)} and ${entryPoint} are both entry points for '${name}'.`);
      found.set(name, entryPoint);
      entries.push({
        entryPoint,
        outfile: join(packageDir, OUTPUT_DIR, group.dir, `${name}.js`),
        globalName: `${ROOT_NAMESPACE}.${group.namespace}.${toPascalCase(name)}`,
      });
    }
  }
  return entries;
}

export function buildOptions(entry, { dev = false } = {}) {
  return {
    entryPoints: [entry.entryPoint],
    outfile: entry.outfile,
    globalName: entry.globalName,
    alias: { "@ogz/crm-entities": ENTITIES_SOURCE },
    bundle: true,
    format: "iife",
    platform: "browser",
    target: "es2020",
    charset: "utf8",
    minify: !dev,
    sourcemap: dev ? "inline" : false,
    legalComments: "none",
    logLevel: "info",
  };
}

/** An empty entry list means the build would silently produce nothing to deploy. */
export function requireEntries(entries) {
  if (entries.length === 0) throw new Error("crm-client: no entry points found in src/forms or src/commands.");
  return entries;
}

/** Bundles every entry; rejects on the first esbuild error. `write: false` keeps output in memory. */
export async function buildEntries(entries, { dev = false, write = true } = {}) {
  // In-memory builds (tests) report errors through the rejected promise only.
  const quiet = write ? {} : { logLevel: "silent" };
  return Promise.all(entries.map((entry) => esbuild.build({ ...buildOptions(entry, { dev }), write, ...quiet })));
}

async function main() {
  const args = new Set(process.argv.slice(2));
  const dev = args.has("--dev");
  const watch = args.has("--watch");

  const entries = requireEntries(findEntries());

  if (watch) {
    const contexts = await Promise.all(entries.map((entry) => esbuild.context(buildOptions(entry, { dev }))));
    await Promise.all(contexts.map((ctx) => ctx.watch()));
    console.log(`crm-client: watching ${entries.length} entry point(s)...`);
    return;
  }

  await buildEntries(entries, { dev });
  for (const entry of entries) console.log(`  ${entry.outfile}  ->  ${entry.globalName}`);
}

// Run only when executed directly (`node build.mjs`), not when imported by tests.
// Any error ends the process with a non-zero exit code, so callers never use stale bundles.
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    await main();
  } catch (error) {
    console.error(error instanceof Error ? error.message : error);
    process.exitCode = 1;
  }
}
