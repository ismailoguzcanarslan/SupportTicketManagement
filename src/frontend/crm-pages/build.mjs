// Builds every dialog page into two Web Resources:
//
//   src/pages/<name>/index.tsx  ->  dist/webresources/pages/<name>.js    (React, Fluent UI and the page)
//                                   dist/webresources/pages/<name>.html  (loads <name>.js next to it)
//
// Deployed as ogz_/pages/<name>.html and ogz_/pages/<name>.js (see deployment.manifest.json); opened from
// form scripts with Dialogs.open(<definition>), which uses ogz_/pages/<name>.html.
//
// Usage: node build.mjs [--dev]

import { existsSync, mkdirSync, readdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import * as esbuild from "esbuild";

const PACKAGE_DIR = dirname(fileURLToPath(import.meta.url));
const OUTPUT_DIR = join("dist", "webresources", "pages");
const ENTITIES_SOURCE = join(PACKAGE_DIR, "..", "crm-entities", "src", "index.ts");

/** Pages in sorted order: folders under src/pages with an index.tsx. */
export function findPages(packageDir = PACKAGE_DIR) {
  const dir = join(packageDir, "src", "pages");
  if (!existsSync(dir)) return [];
  return readdirSync(dir, { withFileTypes: true })
    .filter((item) => item.isDirectory() && existsSync(join(dir, item.name, "index.tsx")))
    .map((item) => item.name)
    .sort()
    .map((name) => ({
      name,
      entryPoint: join(dir, name, "index.tsx"),
      script: join(packageDir, OUTPUT_DIR, `${name}.js`),
      html: join(packageDir, OUTPUT_DIR, `${name}.html`),
    }));
}

/** The HTML web resource of a page. The script path is relative: both live in the ogz_/pages/ folder. */
export function pageHtml(name) {
  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>${name}</title>
  <style>html, body, #root { height: 100%; margin: 0; }</style>
</head>
<body>
  <div id="root"></div>
  <script src="${name}.js"></script>
</body>
</html>
`;
}

export function buildOptions(page, { dev = false } = {}) {
  return {
    entryPoints: [page.entryPoint],
    outfile: page.script,
    alias: { "@ogz/crm-entities": ENTITIES_SOURCE },
    bundle: true,
    format: "iife",
    platform: "browser",
    target: "es2020",
    jsx: "automatic",
    charset: "utf8",
    minify: !dev,
    sourcemap: dev ? "inline" : false,
    define: { "process.env.NODE_ENV": JSON.stringify(dev ? "development" : "production") },
    legalComments: "none",
    logLevel: "info",
  };
}

export async function buildPages(pages, { dev = false, write = true } = {}) {
  if (pages.length === 0) throw new Error("crm-pages: no pages found in src/pages.");
  const quiet = write ? {} : { logLevel: "silent" };
  const results = await Promise.all(pages.map((page) => esbuild.build({ ...buildOptions(page, { dev }), write, ...quiet })));
  if (write) {
    for (const page of pages) {
      mkdirSync(dirname(page.html), { recursive: true });
      writeFileSync(page.html, pageHtml(page.name), "utf8");
    }
  }
  return results;
}

async function main() {
  const dev = process.argv.includes("--dev");
  const pages = findPages();
  await buildPages(pages, { dev });
  for (const page of pages) console.log(`  ${page.html}  +  ${page.script}`);
}

// Run only when executed directly (`node build.mjs`), not when imported by tests.
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    await main();
  } catch (error) {
    console.error(error instanceof Error ? error.message : error);
    process.exitCode = 1;
  }
}
